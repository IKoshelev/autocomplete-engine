namespace Autocomplete.DataStructures;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

internal class Program
{
    const string searchesFilePath = ".\\generated_searches.txt";
    const string autocompleteQueriesFilePath = ".\\generated_autocomplete_queries.txt";
    const string executionLogFile = ".\\execution_log_file.txt";
    const int preloadSearchesCount = 5_000_000;
    const int searchIncomingBatchSize = 30_000;
    const int autocompleteQueryIncomingBatchSize = 90_000;

    private static void Main(string[] args)
    {
        GenerateSearchesAndLetterCombinations();

        var provider = new AutocompleteProvider(isSingleTrieMode: false);

        PrepopulateProvider(provider, preloadSearchesCount);

        GC.Collect();

        RunLoopOfUpsertBatchProcessAutocompleteBatch(provider);
    }

    private static Random _random = new Random();

    private static int[] rankingDeltas = new[] { 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 10 };

    private static int rankingDeltasLength = rankingDeltas.Length;

    private static int rankingIndex = 0;

    private static int getTypicalRankingDelta()
    {
        rankingIndex += 1;
        rankingIndex %= rankingDeltasLength;
        return rankingDeltas[rankingIndex];
    }

    private static void RunLoopOfUpsertBatchProcessAutocompleteBatch(
        AutocompleteProvider provider)
    {
        var searches = File.ReadLines(searchesFilePath)
                                .Skip(preloadSearchesCount)
                                .Take(3_000_000)
                                .ToArray()
                                .AsEnumerable().GetEnumerator();
        searches.MoveNext();

        var autocompleteQueries = File.ReadLines(autocompleteQueriesFilePath)
                                    .Take(10_000_000)
                                    .ToArray()
                                    .AsEnumerable()
                                    .GetEnumerator();

        autocompleteQueries.MoveNext();

        using var logFile = new FileStream(executionLogFile, FileMode.Create);
        using var logWriter = new StreamWriter(logFile);
        int cycle = 0;
        while (true)
        {
            cycle += 1;
            var stopWatchUpsert = new System.Diagnostics.Stopwatch();
            stopWatchUpsert.Start();
            for (int count = 0; count < searchIncomingBatchSize; count++)
            {
                provider.UpsertRanking(searches.Current, getTypicalRankingDelta());
                searches.MoveNext();
            }
            stopWatchUpsert.Stop();

            var stopWatchLookup = new System.Diagnostics.Stopwatch();
            stopWatchLookup.Start();
            string lastQuery = null;
            Ranking[] lastResult = null;
            for (int count = 0; count < autocompleteQueryIncomingBatchSize; count++)
            {
                lastQuery = autocompleteQueries.Current;
                autocompleteQueries.MoveNext();
                lastResult = provider.LookupAutocomplete(lastQuery);
            }
            stopWatchLookup.Stop();
            
            var cycleResult = $"""
                                Completed cycle {cycle}; upsert: {searchIncomingBatchSize}, lookup: {autocompleteQueryIncomingBatchSize}
                                Cycle took: {stopWatchUpsert.Elapsed + stopWatchLookup.Elapsed}
                                Upsert: {stopWatchUpsert.Elapsed}
                                Lookup: {stopWatchLookup.Elapsed}
                                Memory usage: {GetCurrentMemoryUsage()}
                                Search ranking count: {provider.GetAmountOfRankings()}
                                Last autocomplete query: {lastQuery}
                                Last result: {JsonSerializer.Serialize(lastResult)}

                                """;

            logWriter.WriteLine(cycleResult);
            Console.WriteLine($"Loop compete in {stopWatchUpsert.Elapsed + stopWatchLookup.Elapsed}");
        }
    }

    private static void PrepopulateProvider(AutocompleteProvider provider, int preload_searches_count)
    {
        var searchesArr = File.ReadLines(searchesFilePath).Take(preload_searches_count).ToArray();
        var searches = ((IEnumerable<string>)searchesArr).GetEnumerator();
        searches.MoveNext();

        //Console.WriteLine($"memory usage after datasets loaded {GetCurrentMemoryUsage()} mb");

        var stopWatch = new System.Diagnostics.Stopwatch();
        stopWatch.Start();

        for (int count = 0; count < preload_searches_count; count++)
        {
            provider.UpsertRanking(searches.Current, getTypicalRankingDelta());
            searches.MoveNext();
        }
        stopWatch.Stop();
        Console.WriteLine($"Pre-populated with {preload_searches_count} searches in {stopWatch.Elapsed}; memory usage = {GetCurrentMemoryUsage()} mb");
    }

    private static long GetCurrentMemoryUsage()
    {
        return Process.GetCurrentProcess().PrivateMemorySize64 / (1024 * 1024);
    }

    private static void GenerateSearchesAndLetterCombinations()
    {
        if (!File.Exists(searchesFilePath))
        {
            Console.WriteLine($"Generating ${searchesFilePath}");
            var englishWords = File
                .ReadAllLines(".\\words_3pluschars_random_order.txt")
                .ToArray();

            var searches = Enumerable.Range(0, 10_000_000)
                .Select((_) =>
                {
                    var wordCount = _random.Next(1, 4);
                    var words = Enumerable.Range(0, wordCount)
                        .Select((_) => 
                        {
                            var index =  GetSudoPowerlawRandom(_random, upperBound: englishWords.Length - 1);
                            return englishWords[index];
                        })
                        .Distinct()
                        .OrderBy(x => x);

                    return String.Join(" ", words);
                });

            File.WriteAllLines(searchesFilePath, searches);
        }

        if (!File.Exists(autocompleteQueriesFilePath))
        {
            Console.WriteLine($"Generating ${autocompleteQueriesFilePath}");
            var words = File.ReadAllLines(searchesFilePath)
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

            File.WriteAllLines(autocompleteQueriesFilePath, autocompleteLetterQueries);
        }
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
            var x = x_min * Math.Pow((1 - r), (-1 / (alpha - 1))) - 1;
            x = Math.Floor(x * scale);
            if (x > upperBound)
            {
                continue;
            }
            return (int)x;
        }
    }
}
