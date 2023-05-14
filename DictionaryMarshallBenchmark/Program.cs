using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MyBenchmarks
{
    public class Md5VsSha256
    {
        private const int N = 1_000_000;
        private Random _random = new(456457275);

        private (int, string)[] _data;

        private int _dataCursor = 0;
        private (int, string) GetNextDatum()
        {
            _dataCursor += 1;
            _dataCursor %= 1_000_000;
            return _data[_dataCursor];
        }

        private Dictionary<int, string> DictNormal = new();
        private Dictionary<int, string> DictMarshall = new();

        public Md5VsSha256()
        {
            _data = Enumerable.Range(0, N)
                .Select(x => (x, x.ToString()))
                .OrderBy(x => _random.NextInt64())
                .ToArray();
        }

        [Benchmark]
        public string Normal()
        {
            var data = GetNextDatum();
            if(DictNormal.TryGetValue(data.Item1, out var res))
            {
                return res;
            }
            DictNormal.Add(data.Item1, data.Item2);
            return data.Item2;
        }

        [Benchmark]
        public string Marshall()
        {
            var data = GetNextDatum();
            ref string? value = ref CollectionsMarshal.GetValueRefOrAddDefault(DictMarshall, data.Item1, out bool exists);
            if (!exists)
            {
                value = data.Item2;
            }
            return value;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Md5VsSha256>();
        }
    }
}