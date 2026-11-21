using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 08 · LINQ: FullJoin and joins without a result selector (.NET 11); LeftJoin/RightJoin date from .NET 10
// Documentation: docs/en/08-linq-joins.md · docs/fr/08-jointures-linq.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E08_LinqJoins
{
    public record Customer(int Id, string Name);
    public record Order(string Number, int CustomerId, decimal Amount);
    public record WarehouseStock(string Sku, int Quantity);
    public record CatalogItem(string Sku, string Label);

    static readonly Customer[] Customers = [new(1, "Smith"), new(2, "Jones"), new(3, "Brown")];
    static readonly Order[] Orders = [new("O-001", 1, 42.50m), new("O-002", 1, 19.90m), new("O-003", 3, 7.00m), new("O-004", 9, 99.00m)];

    static readonly CatalogItem[] Catalog = [new("COFFEE", "Ground coffee"), new("TEA", "Green tea"), new("MUG", "Stoneware mug")];
    static readonly WarehouseStock[] Stock = [new("COFFEE", 120), new("MUG", 8), new("FILTER", 300)];

    public static void Run()
    {
        Title("08 · LINQ: FullJoin and joins without a selector (.NET 11)");

        // ── Before .NET 10: the left outer join incantation GroupJoin + SelectMany + DefaultIfEmpty ──
        Section("Before .NET 10: GroupJoin + SelectMany + DefaultIfEmpty");
        var before = Customers
            .GroupJoin(Orders, c => c.Id, o => o.CustomerId, (customer, orders) => new { customer, orders })
            .SelectMany(x => x.orders.DefaultIfEmpty(), (x, o) => (x.customer.Name, Number: o?.Number ?? "(none)"));
        foreach (var (name, number) in before) Line($"{name,-8} {number}");

        // ── .NET 10: LeftJoin ──────────────────────────────────────────────────────────────────────
        Section(".NET 10: Customers.LeftJoin(Orders, c => c.Id, o => o.CustomerId, (customer, order) => …)");
        var left = Customers.LeftJoin(Orders, c => c.Id, o => o.CustomerId,
            (customer, order) => (customer.Name, Number: order?.Number ?? "(none)")).ToList();
        foreach (var (name, number) in left) Line($"{name,-8} {number}");
        Check(left.SequenceEqual(before), "exactly the same result as the old incantation");
        Check(left.Contains(("Jones", "(none)")), "Jones, with no order, still shows up (that is what a LEFT JOIN does)");

        // .NET 11: without a result selector, you get (Outer, Inner) tuples directly.
        Section(".NET 11: without a selector, tuples to deconstruct");
        foreach (var (customer, order) in Customers.LeftJoin(Orders, c => c.Id, o => o.CustomerId))
            Line($"{customer.Name,-8} {order?.Amount.ToString("0.00 €") ?? "—"}");

        Section(".NET 11: RightJoin without a selector, every order even when the customer is missing");
        foreach (var (customer, order) in Customers.RightJoin(Orders, c => c.Id, o => o.CustomerId))
            Line($"{order.Number}  customer: {customer?.Name ?? $"#{order.CustomerId} not found"}");
        Check(Customers.RightJoin(Orders, c => c.Id, o => o.CustomerId).Count(x => x.Item1 is null) == 1,
            "order O-004 (customer 9 does not exist) is kept, with a null customer");

        // ── FullJoin: reconcile two sources (catalog and warehouse) ───────────────────────────────
        Section("Before: a full join = left join + the right-hand orphans, by hand");
        var fullBefore = Catalog
            .GroupJoin(Stock, a => a.Sku, s => s.Sku, (a, s) => new { a, s })
            .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => (Sku: x.a.Sku, Item: (CatalogItem?)x.a, Stock: s))
            .Concat(Stock.Where(s => !Catalog.Any(a => a.Sku == s.Sku)).Select(s => (s.Sku, Item: (CatalogItem?)null, Stock: (WarehouseStock?)s)))
            .ToList();
        Line($"{fullBefore.Count} rows, in two passes and one Any() per stock item");

        Section(".NET 11: Catalog.FullJoin(Stock, a => a.Sku, s => s.Sku)");
        var reconciliation = Catalog.FullJoin(Stock, a => a.Sku, s => s.Sku).ToList();
        foreach (var (item, stock) in reconciliation)
        {
            string diagnosis = (item, stock) switch
            {
                (not null, not null) => $"OK, {stock.Quantity} in stock",
                (not null, null) => "in the catalog but never received in the warehouse",
                (null, not null) => "in the warehouse but missing from the catalog!",
                (null, null) => "impossible",
            };
            Line($"{item?.Sku ?? stock!.Sku,-7} {diagnosis}");
        }
        Check(reconciliation.Count == 4 && reconciliation.Count == fullBefore.Count, "4 rows: COFFEE, TEA, MUG and FILTER, like the manual version");

        Section(".NET 11: Join without a selector, with a key comparer (case-insensitive SKUs)");
        WarehouseStock[] lowercaseStock = [new("coffee", 120)];
        int found = Catalog.Join(lowercaseStock, a => a.Sku, s => s.Sku, StringComparer.OrdinalIgnoreCase).Count();
        Check(found == 1, "Join(…, StringComparer.OrdinalIgnoreCase) matches COFFEE and coffee");
    }
}
