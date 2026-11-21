# 13 · Text and Unicode: `Rune` in `string`, `Utf16.IsValid`, `Base64`, `Regex AnyNewLine`, `StringBuilder.MoveChunks` (.NET 11)

🇫🇷 [Version française](../fr/13-texte.md)

> **In one sentence**: a series of small APIs that remove everyday workarounds around text.
>
> Code: [`E13_Text.cs`](../../src/DotNet11Features.Examples/Examples/E13_Text.cs) ·
> Run: `dotnet run -c Release --project src/DotNet11Features.Examples -- text`

---

## 1. `string` accepts `Rune`

### The idea, simply

In .NET, a `char` takes 16 bits. Enough for "é" or "€", but **not for 🍵** (U+1F375): this emoji takes **two
`char`**, a "surrogate pair". A single `char` cannot represent it.

`System.Text.Rune` (since .NET Core 3.0) represents **a whole Unicode character**, emoji included. But `string` could
not search for a `Rune`: you had to turn it back into a `string` every time.

| Before | .NET 11 |
|---|---|
| `review.Contains(tea.ToString())` | `review.Contains(tea)` |
| `review.Replace(tea.ToString(), "☕")` | `review.Replace(tea, new Rune('☕'))` |
| `review.Split(tea.ToString())` | `review.Split(tea, StringSplitOptions.TrimEntries)` |

Overloads added: `Contains`, `StartsWith`, `EndsWith`, `IndexOf`, `LastIndexOf` (with or without
`StringComparison`), `Replace`, `Split`, `Trim`, `TrimStart`, `TrimEnd`, plus `TextInfo.ToUpper/ToLower(Rune)`. And
`char.Equals('É', 'é', StringComparison.CurrentCultureIgnoreCase)` to compare two `char` with a rule.

---

## 2. `Utf16.IsValid`: spot a string cut in two

Cutting a string at the wrong place (`text[..9]`, a database truncation, message splitting) can **separate the two
halves of an emoji**. The resulting string is invalid: it shows "�" and some encoders throw.

```csharp
string truncated = review[..9];                         // cuts 🍵 in two
Utf16.IsValid(truncated)                                // false
Utf16.IndexOfInvalidSubsequence(truncated)              // 8: the orphan half
```

Before, you walked the `char` values testing `char.IsHighSurrogate` / `IsLowSurrogate`. Both methods are vectorized
(SIMD). Same for UTF-8 with `Utf8.IndexOfInvalidSubsequence`.

---

## 3. `Base64` can produce strings

.NET had **two** Base64 APIs:

- `Convert.ToBase64String` / `FromBase64String`: simple, but designed for `byte[]` and `string`;
- `System.Buffers.Text.Base64`: very fast, on spans, but **UTF-8 only** (`byte` to `byte`).

.NET 11 completes the latter with `char` and `string` versions: `EncodeToString`, `EncodeToChars`,
`TryEncodeToChars`, `DecodeFromChars`, `TryDecodeFromChars`… One API for every case, including writing into a stack
buffer with no allocation at all:

```csharp
Span<char> buffer = stackalloc char[16];
Base64.TryEncodeToChars(key, buffer, out int written);   // zero allocation
```

---

## 4. `RegexOptions.AnyNewLine`

In .NET, `^` and `$` in `Multiline` mode only recognize `\n`. A Windows file (`\r\n`) makes `$` fail: right before
`\n` sits a `\r`, not the expected end of line.

```csharp
string log = "line1\r\nline2\rline3 line4";
Regex.Matches(log, @"^line\d$", RegexOptions.Multiline).Count                            // 0 (also checked on .NET 10)
Regex.Matches(log, @"^line\d$", RegexOptions.Multiline | RegexOptions.AnyNewLine).Count  // 4
```

With `AnyNewLine`, `^`, `$` and `.` treat **all** Unicode newlines as line ends (the example checks `\r\n`, `\r`,
`\n` and U+2028). The usual workaround (`\r?$`) only covered the Windows case.

---

## 5. `StringBuilder.MoveChunks`

A `StringBuilder` stores its text in chained **chunks**. To transfer its content to another `StringBuilder` (hand it
to another component, reset a reused buffer…), we did `new StringBuilder(source.ToString())`: a whole string, then a
second copy.

```csharp
var moved = StringBuilder.MoveChunks(source);   // the source is emptied, its chunks are moved as they are
```

---

## 6. Output of the example

```
   ▸ After: Contains, IndexOf, Replace, Split, Trim… take a Rune directly
     review.Contains(tea) = True · review.IndexOf(tea) = 8
     review.Replace(tea, new Rune('☕')) = Service ☕ perfect ☕ recommended
     review.Split(tea, TrimEntries) = [Service | perfect | recommended]
   ▸ Utf16.IsValid: spot a string cut in the middle of an emoji
     "Service 🍵 "[..9] → IsValid = False, first invalid character at index 8
   ▸ Base64: the high-performance class (System.Buffers.Text) can now produce strings
     Convert.ToBase64String = 3q2+7yAm · Base64.EncodeToString = 3q2+7yAm
     Base64.TryEncodeToChars into a stack buffer: 3q2+7yAm (zero allocation)
   ▸ Regex: RegexOptions.AnyNewLine recognizes \r\n, \r, \n,  …
     ^line\d$ in Multiline: 0 match(es); with AnyNewLine: 4
   ▸ StringBuilder.MoveChunks: transfer the content without copying it
     copy through ToString: 542 KB allocated · MoveChunks: 48 bytes
```

## 7. Pitfalls and limits

1. **`IndexOf(Rune)` returns an index in `char`**, not in Runes: 8 in the example, the position of the first of the
   emoji's two `char`. That is consistent with `Substring` and `[..]` ranges.
2. **`AnyNewLine` also changes `.`**, which no longer matches any newline (unless `Singleline` is set).
3. **`MoveChunks` empties the source**: it stays usable, but its `Length` is 0.

## Sources

- [What's new in .NET 11 libraries — Strings, Base64, Unicode, Regex](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
