using System.Buffers;
using System.Text;
using System.Text.Json;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 09 · New streams: StringStream, ReadOnlyMemoryStream, WritableMemoryStream, ReadOnlySequenceStream
// Documentation: docs/en/09-streams.md · docs/fr/09-flux.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E09_Streams
{
    static long Allocations(Action action)
    {
        action();                                              // warm-up
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    static string Kb(long bytes) => $"{bytes / 1024.0,8:N0} KB";

    public static void Run()
    {
        Title("09 · New Stream types (.NET 11)");

        // ── 1. A string passed to an API that expects a Stream ────────────────────────────────────
        string json = Data.Text(Data.OrdersJson(5_000));
        Line($"A {json.Length / 1024:N0} K-character JSON string to pass to an API that expects a Stream");

        Section("Before: new MemoryStream(Encoding.UTF8.GetBytes(text)) → full copy as bytes");
        long bytesBefore = Allocations(() =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            CountBytes(stream);
        });
        Line($"memory allocated: {Kb(bytesBefore)}");

        Section("After: new StringStream(text, Encoding.UTF8) → encoded as it is read");
        long bytesAfter = Allocations(() =>
        {
            using var stream = new StringStream(json, Encoding.UTF8);
            CountBytes(stream);
        });
        Line($"memory allocated: {Kb(bytesAfter)}");
        using (var check = new StringStream(json, Encoding.UTF8))
        {
            var orders = JsonSerializer.Deserialize<List<Data.Order>>(check)!;
            Check(orders.Count == 5_000, "JsonSerializer.Deserialize reads the StringStream directly");
        }
        Check(bytesAfter * 10 < bytesBefore, "StringStream allocates at least 10 times less than GetBytes + MemoryStream");

        // ── 2. A memory region (not an array) seen as a Stream ──────────────────────────────────────
        Section("Before: MemoryStream requires a byte[] → ToArray() on a ReadOnlyMemory<byte> = a copy");
        byte[] file = Encoding.UTF8.GetBytes(json);
        ReadOnlyMemory<byte> slice = file.AsMemory(1_000, 100_000);   // a piece of a larger buffer
        long copy = Allocations(() => { using var s = new MemoryStream(slice.ToArray()); CountBytes(s); });
        Line($"memory allocated: {Kb(copy)}");

        Section("After: new ReadOnlyMemoryStream(slice) → no copy, read-only");
        long noCopy = Allocations(() => { using var s = new ReadOnlyMemoryStream(slice); CountBytes(s); });
        Line($"memory allocated: {Kb(noCopy)}");
        using (var s = new ReadOnlyMemoryStream(slice))
        {
            Check(!s.CanWrite && s.Length == 100_000, "the stream is read-only and exactly the size of the slice");
        }
        Check(noCopy < 1024, "the slice is not copied (less than 1 KB allocated)");

        // ── 3. Write into a supplied fixed-size buffer ───────────────────────────────────────────────
        Section("WritableMemoryStream: write into a rented buffer that never grows");
        byte[] rented = ArrayPool<byte>.Shared.Rent(256);
        try
        {
            using var output = new WritableMemoryStream(rented.AsMemory(0, 256));
            JsonSerializer.Serialize(output, new { Number = "ORD-2026-000042", Amount = 42.5m });
            Line($"{output.Position} bytes written into the rented buffer: {Encoding.UTF8.GetString(rented, 0, (int)output.Position)}");
            Check(output.Position > 0, "the JSON is written straight into the supplied buffer");
            try
            {
                output.Write(new byte[1024]);
                Check(false, "writing past the buffer should have failed");
            }
            catch (NotSupportedException)
            {
                Check(true, "going past the capacity throws NotSupportedException (a MemoryStream would have reallocated)");
            }
        }
        finally { ArrayPool<byte>.Shared.Return(rented); }

        // ── 4. A sequence in chunks (System.IO.Pipelines) ──────────────────────────────────────────
        Section("ReadOnlySequenceStream: read a ReadOnlySequence<byte> (Pipelines) without flattening it");
        ReadOnlySequence<byte> sequence = Chunks(file, chunkSize: 4096);
        Line($"{sequence.Length / 1024:N0} KB sequence in {Math.Ceiling(sequence.Length / 4096.0)} segments");
        long flattened = Allocations(() => { using var s = new MemoryStream(sequence.ToArray()); CountBytes(s); });
        long direct = Allocations(() => { using var s = new ReadOnlySequenceStream(sequence); CountBytes(s); });
        Line($"before, sequence.ToArray() + MemoryStream: {Kb(flattened)}");
        Line($"after, new ReadOnlySequenceStream(seq):    {Kb(direct)}");
        Check(direct < 1024, "the sequence is read segment by segment, without a copy");
    }

    static long CountBytes(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4096];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer)) > 0) total += read;
        return total;
    }

    // Splits an array into chained segments, like the buffers handed out by a PipeReader.
    static ReadOnlySequence<byte> Chunks(byte[] data, int chunkSize)
    {
        Segment? first = null, last = null;
        for (int i = 0; i < data.Length; i += chunkSize)
        {
            var chunk = data.AsMemory(i, Math.Min(chunkSize, data.Length - i));
            if (first is null) first = last = new Segment(chunk, 0);
            else last = last!.Append(chunk);
        }
        return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long start) { Memory = memory; RunningIndex = start; }
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory, RunningIndex + Memory.Length);
            Next = next;
            return next;
        }
    }
}
