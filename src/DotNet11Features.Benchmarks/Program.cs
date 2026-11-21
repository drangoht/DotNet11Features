using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotNet11Features;
using DotNet11Features.Examples;

// Rigorous measurements for the examples where .NET 11 changes performance.
//   dotnet run -c Release --project src/DotNet11Features.Benchmarks -- --filter * --job short    (all, ~6 min)
//   dotnet run -c Release --project src/DotNet11Features.Benchmarks -- --filter *Stream*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>Example 07: compress 4.6 MB of JSON.</summary>
[MemoryDiagnoser]
public class CompressionBenchmarks
{
    private byte[] _json = [];

    [GlobalSetup] public void Setup() => _json = Data.OrdersJson(20_000);

    private byte[] Compress(Func<Stream, Stream> create)
    {
        using var output = new MemoryStream(_json.Length / 4);
        using (var stream = create(output)) stream.Write(_json);
        return output.ToArray();
    }

    [Benchmark(Baseline = true)] public byte[] GZip_Optimal() => Compress(s => new GZipStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Brotli_Optimal() => Compress(s => new BrotliStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Zstandard_Optimal() => Compress(s => new ZstandardStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Zstandard_Quality9() => Compress(s => new ZstandardStream(s, new ZstandardCompressionOptions { Quality = 9 }, leaveOpen: false));
}

/// <summary>Example 07: 1,000 small messages, with and without a dictionary.</summary>
[MemoryDiagnoser]
public class DictionaryBenchmarks
{
    private byte[][] _messages = [];
    private ZstandardDictionary _dictionary = null!;
    private readonly byte[] _buffer = new byte[64 * 1024];

    [GlobalSetup]
    public void Setup()
    {
        var training = Data.JsonMessages(2_000, seed: 1);
        _dictionary = ZstandardDictionary.Create(
            ZstandardDictionary.Train(training.SelectMany(m => m).ToArray(), training.Select(m => m.Length).ToArray(), 4096), quality: 3);
        _messages = Data.JsonMessages(1_000, seed: 2);
    }

    [GlobalCleanup] public void Cleanup() => _dictionary.Dispose();

    [Benchmark(Baseline = true)]
    public long WithoutDictionary()
    {
        long total = 0;
        foreach (var m in _messages) { ZstandardEncoder.TryCompress(m, _buffer, out int n); total += n; }
        return total;
    }

    [Benchmark]
    public long WithDictionary()
    {
        long total = 0;
        foreach (var m in _messages)
        {
            ZstandardEncoder.TryCompress(m, _buffer, out int n, _dictionary, ZstandardCompressionOptions.DefaultWindowLog2);
            total += n;
        }
        return total;
    }
}

/// <summary>Example 08: left and full outer joins, 10,000 customers × 30,000 orders.</summary>
[MemoryDiagnoser]
public class JoinBenchmarks
{
    private (int Id, string Name)[] _customers = [];
    private (int CustomerId, decimal Amount)[] _orders = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _customers = Enumerable.Range(1, 10_000).Select(i => (i, $"Customer {i}")).ToArray();
        _orders = Enumerable.Range(1, 30_000).Select(_ => (random.Next(1, 12_000), (decimal)random.Next(1, 500))).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int Before_GroupJoin_SelectMany() => _customers
        .GroupJoin(_orders, c => c.Id, o => o.CustomerId, (c, os) => (c, os))
        .SelectMany(x => x.os.DefaultIfEmpty(), (x, o) => (x.c.Name, o.Amount))
        .Count();

    [Benchmark]
    public int Net10_LeftJoin() => _customers
        .LeftJoin(_orders, c => c.Id, o => o.CustomerId, (c, o) => (c.Name, o.Amount))
        .Count();

    [Benchmark]
    public int Before_FullJoin_Manual()
    {
        var customerIds = _customers.Select(c => c.Id).ToHashSet();
        return _customers
            .GroupJoin(_orders, c => c.Id, o => o.CustomerId, (c, os) => (c, os))
            .SelectMany(x => x.os.DefaultIfEmpty(), (x, o) => (x.c.Id, o.Amount))
            .Concat(_orders.Where(o => !customerIds.Contains(o.CustomerId)).Select(o => (o.CustomerId, o.Amount)))
            .Count();
    }

    [Benchmark]
    public int Net11_FullJoin() => _customers.FullJoin(_orders, c => c.Id, o => o.CustomerId).Count();
}

/// <summary>Example 09: deserialize a 1.2 MB JSON string through a Stream.</summary>
[MemoryDiagnoser]
public class StreamBenchmarks
{
    private string _json = "";

    [GlobalSetup] public void Setup() => _json = Data.Text(Data.OrdersJson(5_000));

    [Benchmark(Baseline = true)]
    public int Before_MemoryStream_GetBytes()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_json));
        return JsonSerializer.Deserialize<List<Data.Order>>(stream)!.Count;
    }

    [Benchmark]
    public int After_StringStream()
    {
        using var stream = new StringStream(_json, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<Data.Order>>(stream)!.Count;
    }
}

/// <summary>Example 11: write 10,000 events as JSON Lines.</summary>
[MemoryDiagnoser]
public class JsonLinesBenchmarks
{
    private static async IAsyncEnumerable<E11_JsonLines.Event> Source()
    {
        for (int i = 0; i < 10_000; i++) yield return new E11_JsonLines.Event(i, "paid", "ORD-0001");
        await Task.CompletedTask;
    }

    [Benchmark(Baseline = true)]
    public async Task<long> Before_ManualLoop()
    {
        using var output = new MemoryStream();
        byte[] newline = "\n"u8.ToArray();
        await foreach (var e in Source())
        {
            await JsonSerializer.SerializeAsync(output, e);
            await output.WriteAsync(newline);
        }
        return output.Length;
    }

    [Benchmark]
    public async Task<long> After_SerializeAsyncEnumerable()
    {
        using var output = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(output, Source(), topLevelValues: true);
        return output.Length;
    }
}

/// <summary>Example 01: a switch over a union compared with a switch over object.</summary>
[MemoryDiagnoser]
public class UnionBenchmarks
{
    private object[] _objects = [];
    private PaymentMethod[] _unions = [];

    [GlobalSetup]
    public void Setup()
    {
        _objects = Enumerable.Range(0, 1_000)
            .Select(i => (i % 3) switch { 0 => (object)new Card("4242"), 1 => new BankTransfer("FR76"), _ => new Voucher("V", 1) }).ToArray();
        _unions = _objects.Select(o => o switch { Card c => (PaymentMethod)c, BankTransfer t => t, _ => (Voucher)o }).ToArray();
    }

    // Before: the same objects in an object[], switch with a default arm.
    [Benchmark(Baseline = true)]
    public long Before_Object_Switch()
    {
        long total = 0;
        foreach (object p in _objects)
            total += p switch { Card c => c.Last4Digits.Length, BankTransfer => 2, Voucher => 3, _ => throw new InvalidOperationException() };
        return total;
    }

    // After: a union (a struct wrapping the reference), exhaustive switch.
    [Benchmark]
    public long After_Union_Switch()
    {
        long total = 0;
        foreach (PaymentMethod m in _unions)
            total += m switch { Card c => c.Last4Digits.Length, BankTransfer => 2, Voucher => 3 };
        return total;
    }
}

/// <summary>Example 01: the cost of boxing when a union holds a value type.</summary>
[MemoryDiagnoser]
public class UnionBoxingBenchmarks
{
    private readonly int[] _values = Enumerable.Range(0, 1_000).ToArray();

    [Benchmark(Baseline = true)]
    public long Int_Without_Union()
    {
        long total = 0;
        foreach (int v in _values) total += v;
        return total;
    }

    [Benchmark]
    public long Int_In_A_Union()
    {
        long total = 0;
        foreach (int v in _values)
        {
            CustomerId id = v;   // boxed: 24 bytes
            total += id switch { int n => n, Guid => 1, string s => s.Length };
        }
        return total;
    }
}

/// <summary>Example 14: a comparer built by EqualityComparer.Create versus a hand-written class.</summary>
[MemoryDiagnoser]
public class ComparerBenchmarks
{
    private Contact[] _contacts = [];
    private readonly IEqualityComparer<Contact> _class = new ContactByEmailBefore();
    private readonly IEqualityComparer<Contact> _created = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);

    [GlobalSetup]
    public void Setup() => _contacts = Enumerable.Range(0, 10_000)
        .Select(i => new Contact($"contact{i % 4_000}@example.com", "Name", "source")).ToArray();

    [Benchmark(Baseline = true)] public int Before_IEqualityComparerClass() => _contacts.Distinct(_class).Count();
    [Benchmark] public int After_EqualityComparerCreate() => _contacts.Distinct(_created).Count();
}
