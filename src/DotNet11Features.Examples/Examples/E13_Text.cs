using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 13 · Text: string and Rune, Utf16.IsValid, Base64 to strings, Regex AnyNewLine, StringBuilder.MoveChunks
// Documentation: docs/en/13-text.md · docs/fr/13-texte.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E13_Text
{
    public static void Run()
    {
        Title("13 · Text and Unicode (.NET 11)");

        // ── 1. string accepts Rune ──────────────────────────────────────────────────────────────────
        // An emoji such as 🍵 takes TWO chars (a "surrogate pair"): it is not a char.
        // Rune represents a whole Unicode character, but string could not search for one.
        string review = "Service 🍵 perfect 🍵 recommended";
        Rune tea = Rune.GetRuneAt("🍵", 0);

        Section("Before: convert the Rune to a string for every search");
        Line($"review.Contains(tea.ToString()) = {review.Contains(tea.ToString())}");
        Line($"review.Replace(tea.ToString(), \"☕\") = {review.Replace(tea.ToString(), "☕")}");

        Section("After: Contains, IndexOf, Replace, Split, Trim… take a Rune directly");
        Line($"review.Contains(tea) = {review.Contains(tea)} · review.IndexOf(tea) = {review.IndexOf(tea)}");
        Line($"review.Replace(tea, new Rune('☕')) = {review.Replace(tea, new Rune('☕'))}");
        string[] parts = review.Split(tea, StringSplitOptions.TrimEntries);
        Line($"review.Split(tea, TrimEntries) = [{string.Join(" | ", parts)}]");
        Check(review.Contains(tea) && parts.Length == 3, "search and split on a character outside the basic plane, with no intermediate allocation");
        Check('É'.Equals('é', StringComparison.CurrentCultureIgnoreCase), "char.Equals('É', 'é', StringComparison…): compare two chars with a comparison rule");

        // ── 2. Validate UTF-16 ──────────────────────────────────────────────────────────────────────
        Section("Utf16.IsValid: spot a string cut in the middle of an emoji");
        string truncated = review[..9];   // cuts the surrogate pair of 🍵 in two
        Line($"\"{review[..11]}\"[..9] → IsValid = {Utf16.IsValid(truncated)}, first invalid character at index {Utf16.IndexOfInvalidSubsequence(truncated)}");
        Check(Utf16.IsValid(review) && !Utf16.IsValid(truncated), "before, you had to walk the chars and test char.IsSurrogatePair yourself");

        // ── 3. Base64 to and from strings ───────────────────────────────────────────────────────────
        Section("Base64: the high-performance class (System.Buffers.Text) can now produce strings");
        byte[] key = [0xDE, 0xAD, 0xBE, 0xEF, 0x20, 0x26];
        string before = Convert.ToBase64String(key);
        string after = Base64.EncodeToString(key);
        Line($"Convert.ToBase64String = {before} · Base64.EncodeToString = {after}");
        Span<char> buffer = stackalloc char[16];
        Base64.TryEncodeToChars(key, buffer, out int written);
        Line($"Base64.TryEncodeToChars into a stack buffer: {buffer[..written]} (zero allocation)");
        Check(before == after && Base64.DecodeFromChars(after).AsSpan().SequenceEqual(key), "one API for UTF-8, char and string, both ways");

        // ── 4. Regex: every kind of newline ──────────────────────────────────────────────────────────
        Section("Regex: RegexOptions.AnyNewLine recognizes \\r\\n, \\r, \\n, \\u2028…");
        string log = "line1\r\nline2\rline3" + (char)0x2028 + "line4";
        int without = Regex.Matches(log, @"^line\d$", RegexOptions.Multiline).Count;
        int with = Regex.Matches(log, @"^line\d$", RegexOptions.Multiline | RegexOptions.AnyNewLine).Count;
        Line($"^line\\d$ in Multiline: {without} match(es); with AnyNewLine: {with}");
        Check(without < 4 && with == 4, "before, ^ and $ only knew \\n: a Windows file (\\r\\n) made $ fail");

        // ── 5. StringBuilder.MoveChunks ───────────────────────────────────────────────────────────────
        Section("StringBuilder.MoveChunks: transfer the content without copying it");
        var source = new StringBuilder();
        for (int i = 0; i < 10_000; i++) source.Append("log line ").Append(i).Append('\n');
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var copy = new StringBuilder(source.ToString());   // before: ToString() then another copy
        long copyCost = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var moved = StringBuilder.MoveChunks(source);
        long moveCost = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        Line($"copy through ToString: {copyCost / 1024:N0} KB allocated · MoveChunks: {moveCost} bytes");
        Check(source.Length == 0 && moved.Length == copy.Length, "the source is emptied, the destination takes its chunks as they are");
        Check(moveCost < 1024, "MoveChunks allocates almost nothing, whatever the size");
    }
}
