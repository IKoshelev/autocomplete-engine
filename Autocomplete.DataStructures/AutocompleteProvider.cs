using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using rm.Trie;

namespace Autocomplete.DataStructures;

public class AutocompleteProvider
{
    private readonly Dictionary<string, RankingMetadata> ExistingRankings = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie3Letter = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie4Letter = new();
    private readonly TrieMap<SortedSet<Ranking>> Trie5Letter = new();

    public void UpsertRanking(
        string searchText,
        int delta)
    {
        // we rely on the system not sending us searches less than 3 letters and all lowercase

        //TODO make own Trie with Span support and possible optimization for known key length?

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
            var newRanking = oldRanking with { rank = oldRanking.rank + delta };
            foreach (var set in rankingMetadata.Sets)
            {
                // TODO find more upsert friendly sorted set
                set.Remove(oldRanking);
                set.Add(newRanking);
            }
            rankingMetadata.Ranking = newRanking;
        }
    }

    public Ranking[] LookupRanking(string query, int topNToGet)
    {
        // we expect queries no less than 3 letters
        return query.Length switch
        {
            3 => Trie3Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            4 => Trie4Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            5 => Trie5Letter.ValueBy(query)?.Take(topNToGet).ToArray(),
            _ => Trie5Letter.ValueBy(
                    query.Substring(0, 5)
                // we might want to store words withing ranking upfront?
                )?.Where(x => x.text.Split(' ').Any(word => word.StartsWith(query)))
                .Take(topNToGet).ToArray(),
        } ?? new Ranking[0];
    }

    private RankingMetadata AddNewRanking(
        string searchText,
        int delta)
    {
        // TODO handle negative deltas
        RankingMetadata rankingMetadata = new RankingMetadata(new Ranking(searchText, delta));
        ExistingRankings.Add(searchText, rankingMetadata);

        var start = 0;
        var end = 0;
        var max = searchText.Length - 1;
        while (end < max)
        {
            end += 1;
            if (searchText[end] == ' ')
            {
                AddNewWordsToTries(
                    searchText.Substring(start, end - start),
                    rankingMetadata);
                start = end + 1;
            }
            else if (end == max)
            {
                AddNewWordsToTries(
                    searchText.Substring(start, end - start + 1),
                    rankingMetadata);
            }
        }

        return rankingMetadata;
    }

    private void AddNewWordsToTries(
        string word,
        RankingMetadata rankingMetadata)
    {
        var len = word.Length;
        if (len <= 2)
        {
            return;
        }
        if (len >= 3)
        {
            var key = word.Substring(0, 3);
            AddKeyToTrieAndUpdateMetadata(
                rankingMetadata,
                key,
                Trie3Letter);
        }

        if (len >= 4)
        {
            var key = word.Substring(0, 4);
            AddKeyToTrieAndUpdateMetadata(
                rankingMetadata,
                key,
                Trie4Letter);
        }

        if (len >= 5)
        {
            var key = word.Substring(0, 5);
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

public record Ranking(string text, int rank)
{
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