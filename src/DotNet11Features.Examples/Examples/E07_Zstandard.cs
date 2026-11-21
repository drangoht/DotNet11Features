using System.Diagnostics;
using System.IO.Compression;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 07 · Zstandard built into .NET 11: ZstandardStream, ZstandardEncoder, ZstandardDictionary
// Documentation: docs/en/07-zstandard.md · docs/fr/07-zstandard.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E07_Zstandard
{
    // The same code for every algorithm: only the stream constructor changes.
    static byte[] Compress(byte[] data, Func<Stream, Stream> createStream)
    {
        using var output = new MemoryStream();
        using (var stream = createStream(output)) stream.Write(data);
        return output.ToArray();
    }

    static byte[] Decompress(byte[] compressed, Func<Stream, Stream> createStream)
    {
        using var input = new MemoryStream(compressed);
        using var stream = createStream(input);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    static (double CompressMs, double DecompressMs, int Size, bool RoundTrips) Measure(
        byte[] data, Func<Stream, Stream> compressor, Func<Stream, Stream> decompressor)
    {
        byte[] compressed = Compress(data, compressor);               // warm-up
        byte[] back = Decompress(compressed, decompressor);
        const int Rounds = 5;
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Rounds; i++) compressed = Compress(data, compressor);
        double c = stopwatch.Elapsed.TotalMilliseconds / Rounds;
        stopwatch.Restart();
        for (int i = 0; i < Rounds; i++) back = Decompress(compressed, decompressor);
        double d = stopwatch.Elapsed.TotalMilliseconds / Rounds;
        return (c, d, compressed.Length, back.AsSpan().SequenceEqual(data));
    }

    public static void Run()
    {
        Title("07 · Zstandard (.NET 11)");
        byte[] json = Data.OrdersJson(20_000);
        Line($"Data: 20,000 orders as JSON = {json.Length / 1024.0 / 1024:F1} MB");

        Section("Before: GZip or Brotli (Zstandard required a third-party NuGet package and its native DLL)");
        Section("After: ZstandardStream is used exactly like GZipStream");
        Line($"{"Algorithm",-30} {"Size",10} {"Ratio",7} {"Compress",12} {"Decompress",14}");
        Func<Stream, Stream> readZstd = s => new ZstandardStream(s, CompressionMode.Decompress);
        Func<Stream, Stream> Zstd(int quality) => s => new ZstandardStream(s, new ZstandardCompressionOptions { Quality = quality }, leaveOpen: false);
        var results = new Dictionary<string, (double C, double D, int Size, bool Ok)>
        {
            ["GZip (Optimal)"] = Measure(json, s => new GZipStream(s, CompressionLevel.Optimal), s => new GZipStream(s, CompressionMode.Decompress)),
            ["GZip (SmallestSize)"] = Measure(json, s => new GZipStream(s, CompressionLevel.SmallestSize), s => new GZipStream(s, CompressionMode.Decompress)),
            ["Brotli (Optimal)"] = Measure(json, s => new BrotliStream(s, CompressionLevel.Optimal), s => new BrotliStream(s, CompressionMode.Decompress)),
            ["Zstandard (Optimal)"] = Measure(json, s => new ZstandardStream(s, CompressionLevel.Optimal), readZstd),
            ["Zstandard (quality 6)"] = Measure(json, Zstd(6), readZstd),
            ["Zstandard (quality 9)"] = Measure(json, Zstd(9), readZstd),
            ["Zstandard (quality 12)"] = Measure(json, Zstd(12), readZstd),
            ["Zstandard (quality 19)"] = Measure(json, Zstd(19), readZstd),
        };
        foreach (var (name, r) in results)
            Line($"{name,-30} {r.Size / 1024,7:N0} KB {(double)json.Length / r.Size,6:F1}× {r.C,9:F1} ms {r.D,11:F1} ms");
        Check(results.Values.All(r => r.Ok), "every algorithm gives back exactly the original data");
        var zOpt = results["Zstandard (Optimal)"]; var gzip = results["GZip (Optimal)"];
        var z19 = results["Zstandard (quality 19)"];
        Check(zOpt.C * 2 < gzip.C, "Zstandard (Optimal) compresses at least twice as fast as GZip (Optimal)… but produces a bigger file");
        Check(z19.Size < gzip.Size * 0.8, "Zstandard (quality 19) produces a file at least 20% smaller than GZip (Optimal), at the price of slow compression");
        var zstdDecompressions = results.Where(r => r.Key.StartsWith("Zstandard")).Select(r => r.Value.D).ToArray();
        Check(zstdDecompressions.Max() < zstdDecompressions.Min() * 2, "Zstandard decompression speed barely depends on the chosen quality");

        Section("Small messages: a trained dictionary changes everything");
        // A 300-byte message compresses poorly on its own: there is not enough repetition inside it.
        // A dictionary trained on typical messages provides that repetition up front.
        byte[][] training = Data.JsonMessages(2_000, seed: 1);
        byte[][] messages = Data.JsonMessages(1_000, seed: 2);   // other messages, never seen during training
        byte[] samples = training.SelectMany(m => m).ToArray();
        int[] lengths = training.Select(m => m.Length).ToArray();
        byte[] dictionaryData = ZstandardDictionary.Train(samples, lengths, maxDictionarySize: 4096);
        using ZstandardDictionary dictionary = ZstandardDictionary.Create(dictionaryData, quality: 19);

        long raw = 0, gzipAlone = 0, zstdAlone = 0, zstdDictionary = 0;
        bool allRoundTrip = true;
        byte[] buffer = new byte[64 * 1024], readBack = new byte[64 * 1024];
        foreach (byte[] message in messages)
        {
            raw += message.Length;
            gzipAlone += Compress(message, s => new GZipStream(s, CompressionLevel.Optimal)).Length;
            ZstandardEncoder.TryCompress(message, buffer, out int withoutDict, quality: 19, windowLog2: ZstandardCompressionOptions.DefaultWindowLog2);
            zstdAlone += withoutDict;
            ZstandardEncoder.TryCompress(message, buffer, out int withDict, dictionary, windowLog2: ZstandardCompressionOptions.DefaultWindowLog2);
            zstdDictionary += withDict;
            allRoundTrip &= ZstandardDecoder.TryDecompress(buffer.AsSpan(0, withDict), readBack, out int written, dictionary)
                            && readBack.AsSpan(0, written).SequenceEqual(message);
        }
        Line($"1,000 messages, {raw / 1000} bytes on average, {dictionaryData.Length}-byte dictionary");
        Line($"{"GZip, message by message",-36} {gzipAlone / 1000,5} bytes/message  ({(double)raw / gzipAlone:F1}×)");
        Line($"{"Zstandard without dictionary",-36} {zstdAlone / 1000,5} bytes/message  ({(double)raw / zstdAlone:F1}×)");
        Line($"{"Zstandard with dictionary",-36} {zstdDictionary / 1000,5} bytes/message  ({(double)raw / zstdDictionary:F1}×)");
        Check(allRoundTrip, "every message decompresses with the same dictionary");
        Check(zstdDictionary * 2 < zstdAlone, "the dictionary at least halves the size of small messages");
    }
}
