using System.Diagnostics.CodeAnalysis;

namespace Autocomplete.DataStructures;

internal class Program
{
    private static Random _random = new Random();

    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var set = new SortedSet<Ranking>(new ComparerByRank());

        var s = "this is text search";
        var r1 = new Ranking(1, s);
        var r2 = new Ranking(1, s);
        var r3 = new Ranking(2, s);
        var r4 = new Ranking(3, "abc");
        var r5 = new Ranking(1, "def");


        set.Add(r4);
        set.Add(r1);
        set.Add(r3);
        set.Add(r2);
        set.Add(r5);

        var hashSet = new HashSet<Ranking>(new EqualityComparerByText());

        hashSet.Add(r4);
        hashSet.Add(r1);
        hashSet.Add(r3);
        hashSet.Add(r2);
        hashSet.Add(r5);

        var dict = new Dictionary<string, Ranking>();
        dict.Add(r1.text, r1);
        dict.Add(r4.text, r4);
        dict.Add(r5.text, r5);

        var incoming = dict.GetValueOrDefault(s)!;

        set.Remove(incoming);
        hashSet.Remove(incoming);
    }

    private static void Generate_searches_and_letter_combinations()
    {
        var englishWords = File
            .ReadAllLines(".\\words_3pluschars_random_order.txt")
            .ToArray();

        var searches = Enumerable.Range(0, 10_000_000)
            .Select((_) =>
            {
                var wordcount = _random.Next(1, 4);
                var words = Enumerable.Range(0, wordcount)
                    .Select((_) => englishWords[
                        GetSudoPowerlawRandom(_random, englishWords.Length - 1)]);

                return String.Join(" ", words);
            });

        File.WriteAllLines(".\\generated_searches.txt", searches);

        var words = File.ReadAllLines("./generated_searches.txt")
                        .SelectMany(x => x.Split(" "))
                        .ToArray();

        // check probabilities
        // var res = words
        //             .GroupBy(x => x)
        //             .Select(x => (x.Key, x.Count()))
        //             .OrderByDescending(x => x.Item2)
        //             .ToArray();

        var autocompleteLetterQueries = words.Select(word => 
        {
            // var meanLettercountInAutocompleteQuery = 5;
            // var shift = GetSudoPowerlawRandom(_random, 2, 2);
            // if (_random.NextDouble() >= 0.5)
            // {
            //     shift *= -1;
            // }      
            //var letterCount = meanLettercountInAutocompleteQuery + shift;

            var minLettersInQuery = 3;
            var letterCount = minLettersInQuery + GetSudoPowerlawRandom(_random, 2, 4);

            letterCount = Math.Min(letterCount, word.Length);
            return word.Substring(0, letterCount);
        })
        .ToArray();
        
        // add some strings that are not likely to produce a hit and shuffle
        autocompleteLetterQueries = autocompleteLetterQueries.Concat(
           autocompleteLetterQueries.Take(5_000_000).Select(x => String.Join("", x.Reverse()))
        )
        .OrderBy(_ => _random.NextInt64())
        .ToArray();

        File.WriteAllLines("./generated_autocomplete_queries.txt", autocompleteLetterQueries);
    }

    //https://stackoverflow.com/questions/918736/random-number-generator-that-produces-a-power-law-distribution
    //https://stats.stackexchange.com/questions/173242/random-sample-from-power-law-distribution
    private static int GetSudoPowerlawRandom(Random random, double scale = 50, int upperBound = int.MaxValue)
    {
        var x_min = 1;
        var alpha = 2.5;
        while (true) 
        {
            var r = random.NextDouble();
            var x = x_min * Math.Pow((1 - r),(-1 / (alpha - 1))) - 1;
            x =  Math.Floor(x * scale);
            if (x > upperBound)
            {
                continue;
            }
            return (int)x;
        }
    }
}

public record Ranking(int rank, string text)
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
        var rankComparison = x.rank - y.rank;
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
