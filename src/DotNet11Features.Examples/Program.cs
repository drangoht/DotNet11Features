using DotNet11Features;
using DotNet11Features.Examples;

// Each example runs on its own or all in a row:
//   dotnet run -c Release --project src/DotNet11Features.Examples              → all
//   dotnet run -c Release --project src/DotNet11Features.Examples -- unions    → one (name or number)
//   dotnet run -c Release --project src/DotNet11Features.Examples -- list      → the list
// Every claim of the documentation is checked (✓ / ✗); the exit code is the number of failures.

Console.OutputEncoding = System.Text.Encoding.UTF8;
// Same number formatting on every machine (1,234.5), whatever the system language.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CurrentCulture =
    System.Globalization.CultureInfo.InvariantCulture;

(string Number, string Key, string Title, Func<Task> Run)[] examples =
[
    ("01", "unions", "Unions (C# 15)", Sync(E01_Unions.Run)),
    ("02", "closed", "Closed hierarchies (C# 15)", Sync(E02_ClosedHierarchies.Run)),
    ("03", "collections", "with(...) arguments in [ ] (C# 15)", Sync(E03_CollectionArguments.Run)),
    ("04", "indexers", "Extension indexers (C# 15)", Sync(E04_ExtensionIndexers.Run)),
    ("05", "loops", "Labeled break / continue (C# 15)", Sync(E05_LabeledLoops.Run)),
    ("06", "process", "Process.RunAndCaptureTextAsync", E06_Process.Run),
    ("07", "zstandard", "Built-in Zstandard compression", Sync(E07_Zstandard.Run)),
    ("08", "joins", "LINQ FullJoin and selector-less joins", Sync(E08_LinqJoins.Run)),
    ("09", "streams", "StringStream, ReadOnlyMemoryStream…", Sync(E09_Streams.Run)),
    ("10", "json", "System.Text.Json: unions, closed hierarchies", Sync(E10_JsonUnions.Run)),
    ("11", "jsonl", "JSON Lines: SerializeAsyncEnumerable", E11_JsonLines.Run),
    ("12", "numbers", "Decimal64, TryParsePartial, NextInteger<T>…", Sync(E12_Numbers.Run)),
    ("13", "text", "Rune, Utf16, Base64, Regex AnyNewLine…", Sync(E13_Text.Run)),
    ("14", "comparers", "EqualityComparer<T>.Create, BitArray", Sync(E14_Comparers.Run)),
];

string choice = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";

if (choice == "list")
{
    foreach (var e in examples) Console.WriteLine($"  {e.Number}  {e.Key,-12} {e.Title}");
    return 0;
}

var selection = choice == "all" ? examples : examples.Where(e => e.Number == choice.PadLeft(2, '0') || e.Key == choice).ToArray();
if (selection.Length == 0)
{
    Console.WriteLine($"Unknown example: {choice}. Available examples:");
    foreach (var e in examples) Console.WriteLine($"  {e.Number}  {e.Key,-12} {e.Title}");
    return -1;
}

Console.WriteLine($".NET {Environment.Version} · {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
foreach (var e in selection) await e.Run();

Console.WriteLine();
Console.WriteLine(Display.Failures == 0
    ? $"All checks passed ({selection.Length} example(s))."
    : $"{Display.Failures} check(s) failed.");
return Display.Failures;

static Func<Task> Sync(Action action) => () => { action(); return Task.CompletedTask; };
