using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 05 · Labeled break and continue (C# 15): leave an outer loop without a flag or goto
// Documentation: docs/en/05-labeled-break-continue.md · docs/fr/05-break-continue-etiquetes.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E05_LabeledLoops
{
    // Warehouse: 4 aisles × 5 slots, each cell holds a stock level.
    static readonly int[][] Warehouse =
    [
        [12, 8, 0, 5, 9],
        [7, 7, 7, 7, 7],
        [3, 0, 4, 0, 6],
        [10, 11, 12, 0, 14],
    ];

    // ── Before (1): a "found" boolean tested at every level ──────────────────────────────────────
    static (int Aisle, int Slot)? FirstStockoutWithFlag()
    {
        (int, int)? result = null;
        bool found = false;
        for (int aisle = 0; aisle < Warehouse.Length; aisle++)
        {
            for (int slot = 0; slot < Warehouse[aisle].Length; slot++)
            {
                if (Warehouse[aisle][slot] == 0) { result = (aisle, slot); found = true; break; }
            }
            if (found) break;   // do not forget it, or the search goes on
        }
        return result;
    }

    // ── Before (2): goto, the only direct way… and the least loved ──────────────────────────────
    static (int Aisle, int Slot)? FirstStockoutWithGoto()
    {
        (int, int)? result = null;
        for (int aisle = 0; aisle < Warehouse.Length; aisle++)
            for (int slot = 0; slot < Warehouse[aisle].Length; slot++)
                if (Warehouse[aisle][slot] == 0) { result = (aisle, slot); goto Done; }
        Done:
        return result;
    }

    // ── After (.NET 11): name the loop and leave it directly ────────────────────────────────────
    static (int Aisle, int Slot)? FirstStockout()
    {
        (int, int)? result = null;
        aisles: for (int aisle = 0; aisle < Warehouse.Length; aisle++)
        {
            for (int slot = 0; slot < Warehouse[aisle].Length; slot++)
            {
                if (Warehouse[aisle][slot] == 0) { result = (aisle, slot); break aisles; }
            }
        }
        return result;
    }

    // Labeled continue: move on to the next aisle as soon as a stockout is seen in the current one.
    static List<int> AislesWithoutStockout()
    {
        List<int> complete = [];
        aisles: foreach (var (aisle, index) in Warehouse.Select((a, i) => (a, i)))
        {
            foreach (int stock in aisle)
            {
                if (stock == 0) continue aisles;   // no need to look at the rest of this aisle
            }
            complete.Add(index);
        }
        return complete;
    }

    public static void Run()
    {
        Title("05 · Labeled break and continue (C# 15)");

        Section("Before: a boolean flag, or goto");
        Line($"with a flag: {FirstStockoutWithFlag()}");
        Line($"with goto:   {FirstStockoutWithGoto()}");

        Section("After: aisles: for (…) { for (…) { … break aisles; } }");
        var stockout = FirstStockout();
        Line($"first stockout: aisle {stockout?.Aisle}, slot {stockout?.Slot}");
        Check(stockout == (0, 2), "same result as both versions from before");

        Section("continue aisles; : jump straight to the next aisle");
        var complete = AislesWithoutStockout();
        Line($"aisles without any stockout: {string.Join(", ", complete)}");
        Check(complete.SequenceEqual([1]), "only aisle 1 is complete");
    }
}
