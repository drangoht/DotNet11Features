# NouveautesDotNet11 — what's new in .NET 11 and C# 15, example by example

🇫🇷 [Version française](README.fr.md)

Sixteen runnable examples, each with **the code from before**, **the .NET 11 code**, what really changes,
measurements where relevant, and the pitfalls we hit while writing it.

Tested with SDK `11.0.100`, the final release of .NET 11 shipped on November 10, 2026, pinned in `global.json`.
C# 15 is the default language version for `net11.0`: nothing to add, except for example 15 (preview).

> **Runtime Async**, the biggest runtime change, has its own repository:
> [RuntimeAsyncDemo](https://github.com/drangoht/RuntimeAsyncDemo) (state machine versus runtime, benchmarks,
> call stacks). This repository covers **everything else**.

**Every claim is checked at runtime**: the examples print `✓` or `✗` and the program exits with the number of
failures. `./verify.ps1` runs everything.

Code and console output are in English; every example sheet exists in English ([`docs/en`](docs/en)) and in French
([`docs/fr`](docs/fr)).

---

## 1. Contents

### Language: C# 15

| # | Feature | Before | After | Doc |
|---|---|---|---|---|
| 01 | **Unions** | wrapper class or `OneOf<…>`, `_ => throw` arm | `union PaymentMethod(Card, BankTransfer, Voucher);`, checked `switch` | [01-unions](docs/en/01-unions.md) |
| 02 | **Closed hierarchies** | `abstract` + `_ => throw new UnreachableException()` | `closed record OrderState;` | [02-closed-hierarchies](docs/en/02-closed-hierarchies.md) |
| 03 | **Collection arguments** | `new HashSet<string>(comparer)` + loop | `[with(StringComparer.OrdinalIgnoreCase), .. items]` | [03-collection-arguments](docs/en/03-collection-arguments.md) |
| 04 | **Extension indexers** | `json.GetProperty("a").GetProperty("b")` | `json["a"]["b"]` | [04-extension-indexers](docs/en/04-extension-indexers.md) |
| 05 | **Labeled `break` / `continue`** | boolean flag or `goto` | `aisles: for (…) { … break aisles; }` | [05-labeled-break-continue](docs/en/05-labeled-break-continue.md) |
| 15 | **Memory safety** *(preview)* | whole method `unsafe` | `unsafe(*p)` on the one dangerous expression | [15-memory-safety](docs/en/15-memory-safety.md) |

### Libraries

| # | Feature | Before | After | Doc |
|---|---|---|---|---|
| 06 | **`Process.RunAndCaptureTextAsync`** | ~14 lines and a deadlock trap | `await Process.RunAndCaptureTextAsync("git", ["status"])` | [06-process](docs/en/06-process.md) |
| 07 | **Built-in Zstandard** | third-party package + native DLL | `new ZstandardStream(…)`, trained dictionaries | [07-zstandard](docs/en/07-zstandard.md) |
| 08 | **LINQ `FullJoin`** and selector-less joins | GroupJoin + SelectMany + DefaultIfEmpty + Concat | `catalog.FullJoin(stock, a => a.Sku, s => s.Sku)` | [08-linq-joins](docs/en/08-linq-joins.md) |
| 09 | **`StringStream`, `ReadOnlyMemoryStream`…** | `new MemoryStream(Encoding.UTF8.GetBytes(s))` | `new StringStream(s, Encoding.UTF8)`, no copy | [09-streams](docs/en/09-streams.md) |
| 10 | **JSON: unions, closed hierarchies, per-type naming** | home-made `JsonConverter`, list of `[JsonDerivedType]` | `[JsonUnion(TypeClassifier = …)]`, `InferClosedTypePolymorphism` | [10-json-unions](docs/en/10-json-unions.md) |
| 11 | **JSON Lines** | `SerializeAsync` loop + `\n` | `SerializeAsyncEnumerable(…, topLevelValues: true)` | [11-json-lines](docs/en/11-json-lines.md) |
| 12 | **Numbers** | `0.1 + 0.2 ≠ 0.3`, manual parsing, `NextBytes` | `Decimal64`, `TryParsePartial`, `NextInteger<T>` | [12-numbers](docs/en/12-numbers.md) |
| 13 | **Text and Unicode** | `Contains(rune.ToString())`, `\r?$` | `Contains(rune)`, `Utf16.IsValid`, `RegexOptions.AnyNewLine` | [13-text](docs/en/13-text.md) |
| 14 | **`EqualityComparer<T>.Create(key)`, `BitArray`** | an `IEqualityComparer<T>` class | `EqualityComparer<Contact>.Create(c => c?.Email)` | [14-comparers-bitarray](docs/en/14-comparers-bitarray.md) |

### SDK

| # | Feature | Before | After | Doc |
|---|---|---|---|---|
| 16 | **`#:include` in file-based apps** | a single file, or convert to a project | `#:include models.cs` | [16-file-based-apps](docs/en/16-file-based-apps.md) |

### How each sheet is built

1. **The idea, simply**, with an everyday image when it helps;
2. **How we did it before**, the real code, with its flaws;
3. **What .NET 11 changes**, with a Mermaid diagram when the mechanism lends itself to one;
4. **Side by side** before / after;
5. **Results**: the actual output of the example and, where relevant, benchmarks;
6. **Pitfalls and limits**, including the ones we hit while writing the code.

---

## 2. Key takeaways

- **Unions and closed hierarchies** are the change that will most affect how C# is written: the compiler finally
  checks that a `switch` handles every case. Turn **CS8509 into an error** to benefit from it (see
  `Directory.Build.props`).
- **A union of value types boxes** (24 bytes for an `int`). A union of classes allocates nothing and costs about as
  much as an `object`.
- **A behavior change to test before migrating**: `(decimal)1.23` is now `1.2299999999999999822364316060` instead of
  `1.23` ([example 12](docs/en/12-numbers.md)).
- **Zstandard is not magic** against .NET's GZip (already fast thanks to zlib-ng): its default level is 5× faster but
  produces bigger files. Its real strength is the **dictionary** for small messages (×4 in size, ×1.9 in speed).
- **The new streams** (`StringStream`…) remove whole copies of data: −1.1 MB and −28 % time to deserialize 1.1 MB of
  JSON from a string.
- **Several features are mostly about readability**, with no measurable performance gain:
  `EqualityComparer<T>.Create`, extension indexers, labeled `break`. `FullJoin` is even slower than a well-written
  manual join.

---

## 3. Run

```bash
# Check everything (examples 01-14, then 15 and 16)
./verify.ps1

# Examples 01 to 14, all or one (by number or name)
dotnet run -c Release --project src/DotNet11Features.Examples
dotnet run -c Release --project src/DotNet11Features.Examples -- unions
dotnet run -c Release --project src/DotNet11Features.Examples -- 7
dotnet run -c Release --project src/DotNet11Features.Examples -- list

# Example 15 (C# 15 preview, separate project)
dotnet run -c Release --project src/DotNet11Features.MemorySafety

# Example 16 (file-based app, no project)
dotnet run file-based-app/report.cs -- Lyon

# Benchmarks (BenchmarkDotNet): all in short job (~6 min), or a single class
dotnet run -c Release --project src/DotNet11Features.Benchmarks -- --filter * --job short
dotnet run -c Release --project src/DotNet11Features.Benchmarks -- --filter *Stream*
```

---

## 4. Measured results

BenchmarkDotNet 0.16.0-preview.2, `--job short` (3 iterations) unless stated otherwise, SDK .NET 11.0.100 / runtime
11.0.0, AMD Ryzen 7 9800X3D, Windows 11, measured on 2026-11-21. The short job gives reliable orders of magnitude but
wide error margins: run again without `--job short` for final numbers.

### Unions (example 01), 1,000 values (full job)

| Method | Time | Memory |
|---|---:|---:|
| `object[]` + `switch` with `_ => throw` | 1.74 µs | 0 |
| union of classes + exhaustive `switch` | 1.85 µs | 0 |
| `int` alone (short job) | 0.21 µs | 0 |
| `int` stored in a union (short job) | 1.27 µs | 24,000 B |

### Zstandard (example 07)

| Compressing 4.5 MB of JSON | Time | Size (demo) |
|---|---:|---:|
| GZip (Optimal) | 20.1 ms | 338 KB |
| Brotli (Optimal) | 13.7 ms | 395 KB |
| Zstandard (Optimal) | **4.1 ms** | 434 KB |
| Zstandard (quality 9) | 29.2 ms | 319 KB |

| 1,000 messages of 233 bytes | Time | Average size |
|---|---:|---:|
| Zstandard without dictionary | 3.81 ms | 164 B |
| Zstandard with dictionary | **1.96 ms** | **45 B** |

### LINQ joins (example 08), 10,000 customers × 30,000 orders

| Method | Time | Memory |
|---|---:|---:|
| GroupJoin + SelectMany + DefaultIfEmpty | 2.26 ms | 3.96 MB |
| LeftJoin (.NET 10) | 2.14 ms | 2.84 MB |
| Manual full join (with a `HashSet`) | 1.70 ms | 4.48 MB |
| FullJoin (.NET 11) | 3.44 ms | 3.49 MB |

### Streams (example 09): deserialize 1.1 MB of JSON from a string

| Method | Time | Memory |
|---|---:|---:|
| `MemoryStream(Encoding.UTF8.GetBytes(json))` | 5.73 ms | 5.45 MB |
| `StringStream(json, Encoding.UTF8)` | **4.13 ms** | **4.33 MB** |

### JSON Lines (example 11): 10,000 events

| Method | Time | Memory |
|---|---:|---:|
| `SerializeAsync` loop + `\n` | 1.26 ms | 3.21 MB |
| `SerializeAsyncEnumerable(…, topLevelValues: true)` | **1.04 ms** | **1.38 MB** |

### Comparers (example 14): `Distinct` over 10,000 contacts

| Method | Time | Memory |
|---|---:|---:|
| `IEqualityComparer<Contact>` class | 266 µs | 197.6 KB |
| `EqualityComparer<Contact>.Create(c => c?.Email, …)` | 270 µs | 197.6 KB |

---

## 5. Not covered here

- **Runtime Async**: see [RuntimeAsyncDemo](https://github.com/drangoht/RuntimeAsyncDemo).
- **JIT and runtime**: bounds-check elimination, devirtualization, AVX-VNNI-512, in-process crash reports on Linux,
  x86-64-v2 hardware baseline. Automatic gains, no code to write.
- **ASP.NET Core 11, EF Core 11, .NET MAUI** (CoreCLR by default): outside the scope of this repository.
- **Other APIs** not illustrated: `Process.Signal`, `SafeProcessHandle`, `Dns.ResolveSrv/Mx/Txt`, `X25519DiffieHellman`,
  `Aes.EncryptKeyWrap`, async validation (`AsyncValidationAttribute`), `MemoryCache` metrics, ZIP passwords, GNU
  format for `TarFile`, incremental `CborReader`, SIMD vectors (`Vector128.Zip`…).

---

## Structure

```
NouveautesDotNet11/
├── Directory.Build.props        net11.0 for every project, CS8509 (incomplete switch) turned into an error
├── global.json                  .NET 11 SDK (11.0.100, final release)
├── verify.ps1                   Runs every example, stops at the first failure
├── docs/
│   ├── en/                      One sheet per example (01 to 16), in English
│   └── fr/                      The same sheets in French
├── file-based-app/              Example 16: file-based app with #:include
│   ├── report.cs
│   ├── models.cs
│   └── tools/formatting.cs
└── src/
    ├── DotNet11Features.Examples/         Examples 01 to 14, runnable one by one
    │   ├── Program.cs                     Table of contents and example selection
    │   ├── Display.cs                     Titles, ✓ / ✗, failure counter
    │   ├── Data.cs                        Reproducible data set (orders, catalog)
    │   └── Examples/E01_Unions.cs … E14_Comparers.cs
    ├── DotNet11Features.MemorySafety/     Example 15 (LangVersion preview, isolated project)
    └── DotNet11Features.Benchmarks/       BenchmarkDotNet 0.16 preview (0.15.8 does not recognize .NET 11)
```

## Sources

- [What's new in .NET 11](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview) ·
  [libraries](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries) ·
  [runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime) ·
  [SDK](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/sdk)
- [What's new in C# 15](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)

MIT License.
