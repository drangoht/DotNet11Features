using System.Text.Json;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 04 · Extension indexers (C# 15): add [ ] to a type you do not own
// Documentation: docs/en/04-extension-indexers.md · docs/fr/04-indexeurs-extension.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public record LineItem(string Sku, string Label, int Quantity);

// ── Before: an extension method. Readable, but you lose the [ ] syntax and assignment. ─────────
public static class ExtensionsBefore
{
    public static JsonElement Prop(this JsonElement element, string name) => element.GetProperty(name);
    public static LineItem BySku(this IReadOnlyList<LineItem> lines, string sku) => lines.First(l => l.Sku == sku);
}

// ── After (.NET 11): an extension block (C# 14) can now declare an indexer ──────────────────────
public static class IndexerExtensions
{
    // JsonElement has no indexer by property name (JsonNode does): give it one.
    extension(JsonElement element)
    {
        public JsonElement this[string name] => element.GetProperty(name);
    }

    // A list of order lines, indexed by product code.
    extension(IReadOnlyList<LineItem> lines)
    {
        public LineItem this[string sku] => lines.First(l => l.Sku == sku);
    }

    // Generic: a "from the end" indexer on any sequence.
    extension<T>(IEnumerable<T> source)
    {
        public T this[Index position] => source.ElementAt(position);
    }
}

public static class E04_ExtensionIndexers
{
    const string Json = """
        { "order": { "number": "ORD-2026-0042", "customer": { "name": "Smith", "city": "Lyon" } } }
        """;

    public static void Run()
    {
        Title("04 · Extension indexers (C# 15)");
        using JsonDocument doc = JsonDocument.Parse(Json);
        JsonElement root = doc.RootElement;

        Section("Before: chained GetProperty calls, or an extension method");
        Line(root.GetProperty("order").GetProperty("customer").GetProperty("city").GetString()!);
        Line(root.Prop("order").Prop("customer").Prop("city").GetString()!);

        Section("After: root[\"order\"][\"customer\"][\"city\"]");
        string city = root["order"]["customer"]["city"].GetString()!;
        Line(city);
        Check(city == "Lyon", "the extension indexer chains like a built-in indexer");

        Section("A business indexer on IReadOnlyList<LineItem>");
        IReadOnlyList<LineItem> lines = [new("COFFEE-250G", "Ground coffee", 2), new("GREEN-TEA", "Green tea", 1)];
        Line($"lines[\"GREEN-TEA\"] → {lines["GREEN-TEA"].Label} × {lines["GREEN-TEA"].Quantity}");
        Check(lines["COFFEE-250G"].Quantity == 2, "lookup by product code with the [ ] syntax");
        Check(lines[0].Sku == "COFFEE-250G", "the built-in lines[0] indexer still wins and is unchanged");

        Section("A generic indexer on IEnumerable<T>, with ^1 (from the end)");
        IEnumerable<int> numbers = Enumerable.Range(1, 10).Where(n => n % 3 == 0);
        Line($"numbers = {string.Join(", ", numbers)} ; numbers[^1] = {numbers[^1]}");
        Check(numbers[^1] == 9, "^1 on a LINQ sequence without .ToList()");
    }
}
