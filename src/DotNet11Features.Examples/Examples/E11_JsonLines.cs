using System.Text;
using System.Text.Json;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 11 · JSON Lines: write an async stream, one object per line (.NET 11)
// Documentation: docs/en/11-json-lines.md · docs/fr/11-json-lines.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E11_JsonLines
{
    public record Event(int Sequence, string Type, string Order);

    // A source that produces events over time (database, message queue…).
    static async IAsyncEnumerable<Event> Events(int count)
    {
        string[] types = ["created", "paid", "shipped"];
        for (int i = 1; i <= count; i++)
        {
            if (i % 100 == 0) await Task.Yield();   // simulates an I/O wait from time to time
            yield return new Event(i, types[i % 3], $"ORD-{i / 3 + 1:D4}");
        }
    }

    // ── Before: a loop that serializes each object and then writes the newline by hand ──────────────
    static async Task WriteJsonLinesBefore(Stream output, IAsyncEnumerable<Event> source)
    {
        byte[] newline = "\n"u8.ToArray();
        await foreach (var e in source)
        {
            await JsonSerializer.SerializeAsync(output, e);   // careful: never WriteIndented here
            await output.WriteAsync(newline);
        }
    }

    public static async Task Run()
    {
        Title("11 · JSON Lines with JsonSerializer.SerializeAsyncEnumerable (.NET 11)");

        Section("Before: await foreach + SerializeAsync + \\n by hand");
        using var before = new MemoryStream();
        await WriteJsonLinesBefore(before, Events(3));
        Console.Write(Indent(Encoding.UTF8.GetString(before.ToArray())));

        Section("After: SerializeAsyncEnumerable(stream, source, topLevelValues: true)");
        using var after = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(after, Events(3), topLevelValues: true);
        string jsonl = Encoding.UTF8.GetString(after.ToArray());
        Console.Write(Indent(jsonl));
        Check(after.ToArray().AsSpan().SequenceEqual(before.ToArray()), "byte for byte the same result as the manual loop");
        Check(jsonl.EndsWith('\n') && !jsonl.Contains('\r'), "every line, including the last one, ends with \\n (never \\r\\n, even on Windows)");

        Section("Without topLevelValues: a single JSON array, also written as items arrive");
        using var array = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(array, Events(3));
        Line(Encoding.UTF8.GetString(array.ToArray()));

        Section("Indentation options are ignored: one value = one line, always");
        using var indented = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(indented, Events(2), topLevelValues: true, new JsonSerializerOptions { WriteIndented = true });
        Check(Encoding.UTF8.GetString(indented.ToArray()).Count(c => c == '\n') == 2, "WriteIndented = true does not break the format (2 objects = 2 lines)");

        Section("Round trip: DeserializeAsyncEnumerable(…, topLevelValues: true) reads the file back");
        using var big = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(big, Events(10_000), topLevelValues: true);
        big.Position = 0;
        int read = 0, last = 0;
        await foreach (var e in JsonSerializer.DeserializeAsyncEnumerable<Event>(big, topLevelValues: true))
        {
            read++;
            last = e!.Sequence;
        }
        Line($"{big.Length / 1024} KB written, {read} events read back, last #{last}");
        Check(read == 10_000 && last == 10_000, "the 10,000 events are read back in order");
    }

    static string Indent(string text) => string.Concat(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => $"     {l}\n"));
}
