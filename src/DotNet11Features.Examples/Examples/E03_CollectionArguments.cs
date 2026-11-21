using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 03 · Arguments in collection expressions: [with(...), ...] (C# 15)
// Documentation: docs/en/03-collection-arguments.md · docs/fr/03-arguments-collection.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E03_CollectionArguments
{
    static readonly string[] Tags = ["Promo", "PROMO", "promo", "New", "new", "Sale"];

    // ── Before: as soon as you need a comparer or a capacity, you leave the [ … ] syntax ──────────
    static HashSet<string> UniqueTagsBefore()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tags) result.Add(t);
        return result;
    }

    // ── After: with(...) as the first element passes the arguments to the constructor ──────────────
    static HashSet<string> UniqueTags() => [with(StringComparer.OrdinalIgnoreCase), .. Tags];

    static List<int> ListWithCapacity(int[] source) => [with(capacity: source.Length * 2), .. source];

    public static void Run()
    {
        Title("03 · Arguments in collection expressions (C# 15)");

        Section("Before: new HashSet<string>(StringComparer.OrdinalIgnoreCase) + a loop");
        Line(string.Join(", ", UniqueTagsBefore()));

        Section("After: [with(StringComparer.OrdinalIgnoreCase), .. Tags]");
        var unique = UniqueTags();
        Line(string.Join(", ", unique));
        Check(unique.Count == 3, "6 tags → 3 unique ones, ignoring case");

        Section("Case-insensitive dictionary");
        Dictionary<string, decimal> prices = [with(StringComparer.OrdinalIgnoreCase)];
        prices["COFFEE-250G"] = 6.90m;
        Check(prices["coffee-250g"] == 6.90m, "prices[\"coffee-250g\"] finds the \"COFFEE-250G\" key");

        Section("Capacity reserved up front: fewer reallocations");
        int[] source = Enumerable.Range(1, 1000).ToArray();
        var list = ListWithCapacity(source);
        Check(list.Capacity == 2000, $"[with(capacity: 2000), .. source] → Capacity = {list.Capacity}");

        // Measurement: fill a list one item at a time, with and without reserved capacity.
        long Measure(Func<List<int>> create)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int r = 0; r < 100; r++)
            {
                var l = create();
                for (int i = 0; i < 10_000; i++) l.Add(i);
            }
            return (GC.GetAllocatedBytesForCurrentThread() - before) / 100;
        }
        long without = Measure(() => []);
        long with = Measure(() => [with(capacity: 10_000)]);
        Line($"10,000 Add without capacity: {without,7:N0} bytes allocated (13 successive arrays: 4, 8, 16… 16,384 slots)");
        Line($"10,000 Add with capacity:    {with,7:N0} bytes allocated (a single array)");
        Check(with < without / 2, "reserving the capacity cuts allocations by more than half");
    }
}
