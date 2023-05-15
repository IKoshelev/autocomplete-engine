using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using rm.Trie;

namespace Autocomplete.DataStructures;

public class AutocompleteProvider
{
    public readonly bool IsSingleTrieMode;
    private readonly Dictionary<string, RankingMetadata> ExistingRankings = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie3Letter = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie4Letter = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie5Letter = new();
    
    public AutocompleteProvider(bool isSingleTrieMode = false)
    {
        this.IsSingleTrieMode = isSingleTrieMode;
    }

    public void UpsertRanking(
        string searchText,
        int delta)
    {
        // we rely on the system not sending us searches less than 3 letters and all lowercase

        //TODO make own Trie with Span support and possible optimization for known key length / alphabet?

        // benchmark for marshalling showed no improvement over TryGet/insert ???
        // ref SetsContainingRanking? value = 
        //     ref CollectionsMarshal.GetValueRefOrAddDefault(ExistingRankings, search, out bool exists);

        RankingMetadata rankingMetadata = null;
        if (!ExistingRankings.TryGetValue(searchText, out rankingMetadata))
        {
            rankingMetadata = AddNewRanking(searchText, delta);
        }
        else
        {
            var oldRanking = rankingMetadata.Ranking;
            var newRanking = new Ranking(oldRanking, oldRanking.rank + delta);
            foreach (var set in rankingMetadata.Sets)
            {
                // TODO find more upsert friendly sorted set
                set.Remove(oldRanking);
                set.Add(newRanking);
            }
            rankingMetadata.Ranking = newRanking;
        }
    }

    public int GetAmountOfRankings()
    {
        return this.ExistingRankings.Count();
    }

    public Ranking[] LookupAutocomplete(string query, int topNToGet = 5)
    {
        if (IsSingleTrieMode)
        {
            return Trie3Letter
                .ValueBy(query.Substring(0, 3))?.Where(x => x.AnyWordStartsWith(query))
                .Take(topNToGet).ToArray()
                ?? Array.Empty<Ranking>();
        }

        // we expect queries no less than 3 letters
        return query.Length switch
        {
            3 => Trie3Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            4 => Trie4Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            5 => Trie5Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            _ => Trie5Letter.ValueBy(
                    query.Substring(0, 5)
                )?.Where(x => x.AnyWordStartsWith(query))
                .Take(topNToGet).ToArray(),
        } ?? Array.Empty<Ranking>();
    }

    private RankingMetadata AddNewRanking(
        string searchText,
        int delta)
    {
        // TODO handle negative deltas
        RankingMetadata rankingMetadata = new RankingMetadata(new Ranking(searchText, delta));
        ExistingRankings.Add(searchText, rankingMetadata);

        var words = rankingMetadata.Ranking.words3;
        AddNewWordsToTries(words.word1, rankingMetadata);
        if (words.word2.HasValue)
        {
            AddNewWordsToTries(words.word2.Value, rankingMetadata);
        }
        if (words.word3.HasValue)
        {
            AddNewWordsToTries(words.word3.Value, rankingMetadata);
        }

        return rankingMetadata;
    }

    private void AddNewWordsToTries(
        ReadOnlyMemory<char> wordMem,
        RankingMetadata rankingMetadata)
    {
        var word =  wordMem.Span;
        var len = word.Length;
        if (len <= 2)
        {
            return;
        }
        if (len >= 3)
        {
            var key = word.Slice(0, 3).ToString();
            AddKeyToTrieAndUpdateMetadata(
                rankingMetadata,
                key,
                Trie3Letter);
        }

        if (this.IsSingleTrieMode)
        {
            return;
        }

        if (len >= 4)
        {
            var key = word.Slice(0, 4).ToString();
            AddKeyToTrieAndUpdateMetadata(
                rankingMetadata,
                key,
                Trie4Letter);
        }

        if (len >= 5)
        {
            var key = word.Slice(0, 5).ToString();
            AddKeyToTrieAndUpdateMetadata(
                rankingMetadata,
                key,
                Trie5Letter);
        }
    }

    private static void AddKeyToTrieAndUpdateMetadata(
        RankingMetadata rankingMetadata,
        string key,
        TrieMap<SortedSet<Ranking>> trieForKey)
    {
        var set = trieForKey.ValueBy(key);
        if (set == null)
        {
            set = new SortedSet<Ranking>(new ComparerByRank());
            trieForKey.Add(key, set);
        }
        set.Add(rankingMetadata.Ranking);
        rankingMetadata.Sets.Add(set);
    }

    private class RankingMetadata
    {
        public RankingMetadata(Ranking ranking)
        {
            Ranking = ranking;
        }

        public Ranking Ranking;

        public List<SortedSet<Ranking>> Sets = new();
    }
}

public record Words3(
    ReadOnlyMemory<char> word1,
    ReadOnlyMemory<char>? word2,
    ReadOnlyMemory<char>? word3);

public class Ranking
{
    public readonly string text;
    public int rank;
    public readonly Words3 words3;

    // for JSON only
    public string Text => this.text;
    public int Rank => this.rank;

    public Ranking(string text, int rank)
    {
        this.text = text;
        this.rank = rank;
        this.words3 = FindFist3Words(text.AsMemory());
    }

    public Ranking(Ranking existingRanking, int newRank)
    {
        this.text = existingRanking.text;
        this.rank = newRank;
        this.words3 = existingRanking.words3;
    }

    public bool AnyWordStartsWith(string query)
    {
        if (this.words3.word1.Span.StartsWith(query))
        {
            return true;
        }

        if (this.words3.word2 == null) 
        {
            return false;
        }

        if (this.words3.word2.Value.Span.StartsWith(query))
        {
            return true;
        }
        
        return this.words3.word3?.Span.StartsWith(query) ?? false;
    }

    private static Words3 FindFist3Words(ReadOnlyMemory<char> textMem)
    {
        var text = textMem.Span;
        var start = 0;
        var end = 0;
        var currentWord = 1;
        ReadOnlyMemory<char>? word1 = null;
        ReadOnlyMemory<char>? word2 = null;
        ReadOnlyMemory<char>? word3 = null;
        var max = text.Length - 1;

        while (end < max)
        {
            end += 1;
            ReadOnlyMemory<char>? word = null;
            if (text[end] == ' ')
            {
                word = textMem.Slice(start, end - start);
                start = end + 1;
            }
            else if (end == max)
            {
                word = textMem.Slice(start, end - start + 1);
            }
            if (word.HasValue)
            {
                if (currentWord == 1)
                {
                    word1 = word;
                }
                if (currentWord == 2)
                {
                    word2 = word;
                }
                if (currentWord == 3)
                {
                    word3 = word;
                }

                currentWord += 1;
            }
        }
        return new Words3(word1.Value, word2, word3);
    }

    public override int GetHashCode()
    {
        return text.GetHashCode();
    }
}

public class EqualityComparerByText : IEqualityComparer<Ranking>
{
    public bool Equals(Ranking? x, Ranking? y)
    {
        return x.text.Equals(y.text);
    }

    public int GetHashCode([DisallowNull] Ranking obj)
    {
        return obj.text.GetHashCode();
    }
}

public class ComparerByRank : IComparer<Ranking>
{
    public int Compare(Ranking? x, Ranking? y)
    {
        var rankComparison = y.rank - x.rank;
        if (rankComparison != 0)
        {
            return rankComparison;
        }
        else
        {
            return x.text.CompareTo(y.text);
        }
    }
}