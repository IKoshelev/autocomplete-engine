using System.Diagnostics.CodeAnalysis;

namespace Autocomplete.DataStructures;

internal class Program
{

    //https://stackoverflow.com/questions/918736/random-number-generator-that-produces-a-power-law-distribution
    //https://stats.stackexchange.com/questions/173242/random-sample-from-power-law-distribution
    private static double GeneratePowerlaw()
    {
        // var x_min = 5;// Maximum value
        // //var  = 0.1;// it can't be zero; otherwise X^0^(neg) is 1/0.
        // var alpha = -2.5;    // It has to be negative.
        // //var y = runif(1e7)   // Number of samples

        // var x = new Random().NextDouble();
        // var step1 = ((alpha - 1) / x_min) * Math.Pow((x / x_min), -alpha);

        var x_min = 1;
        var alpha = 2.5;
        var r = new Random().NextDouble();
        return x_min * Math.Pow((1 - r),(-1 / (alpha - 1)));
    }

    private static void Main(string[] args)
    {
        var res = Enumerable.Range(0,1000000)
            .Select(x => GeneratePowerlaw() * 100)
            .Select(x => Math.Floor(x))
            .GroupBy(x => x)
            .Select(x => (x.Key, x.Count()))
            .OrderBy(x => x.Item1)
            .ToArray();

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
