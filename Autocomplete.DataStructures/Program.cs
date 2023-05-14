using System.Diagnostics.CodeAnalysis;

namespace Autocomplete.DataStructures;

internal class Program
{
    private static Random _random = new Random();

    private static void Main(string[] args)
    {
        //Generate_searches_and_letter_combinations();

        var provider = new AutocompleteProvider();
        provider.UpsertRanking("abcdef", +5);
        provider.UpsertRanking("abcdefg foobar", +6);
        provider.UpsertRanking("abcdefgh foobx", +7);
        provider.UpsertRanking("abcdefg foobar", +4);

        var r1 = provider.LookupRanking("abc");
        var r2 = provider.LookupRanking("abcdefg");
        var r3 = provider.LookupRanking("abcdefgh");
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
