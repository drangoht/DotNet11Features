using System.Text;
using System.Text.Json;

namespace DotNet11Features;

/// <summary>Reproducible data set (fixed seed) shared by the examples and the benchmarks.</summary>
public static class Data
{
    public record Product(string Sku, string Label, decimal UnitPrice);
    public record OrderLine(string Sku, int Quantity, decimal UnitPrice);
    public record Order(string Number, int CustomerId, string City, DateOnly Date, string Status, List<OrderLine> Lines);

    static readonly string[] Cities = ["Lyon", "Paris", "Marseille", "Nantes", "Lille", "Bordeaux", "Toulouse", "Rennes"];
    static readonly string[] Statuses = ["draft", "paid", "shipped", "delivered"];

    public static readonly Product[] Catalog =
    [
        new("COFFEE-250G", "Ground coffee 250 g", 6.90m), new("COFFEE-1KG", "Coffee beans 1 kg", 22.50m),
        new("GREEN-TEA", "Green tea", 8.40m), new("BLACK-TEA", "Black tea", 7.90m), new("MUG", "Stoneware mug", 12.00m),
        new("FILTERS", "Paper filters ×100", 3.20m), new("GRINDER", "Hand grinder", 39.00m), new("CHOCO", "Dark chocolate 70 %", 4.10m),
    ];

    public static List<Order> Orders(int count, int seed = 42)
    {
        var random = new Random(seed);
        var orders = new List<Order>(count);
        for (int i = 0; i < count; i++)
        {
            int lineCount = random.Next(1, 5);
            var lines = new List<OrderLine>(lineCount);
            for (int l = 0; l < lineCount; l++)
            {
                var p = Catalog[random.Next(Catalog.Length)];
                lines.Add(new OrderLine(p.Sku, random.Next(1, 6), p.UnitPrice));
            }
            orders.Add(new Order($"ORD-2026-{i + 1:D6}", random.Next(1, 2000), Cities[random.Next(Cities.Length)],
                new DateOnly(2026, 1, 1).AddDays(random.Next(320)), Statuses[random.Next(Statuses.Length)], lines));
        }
        return orders;
    }

    /// <summary>One large JSON document (every order in a single array).</summary>
    public static byte[] OrdersJson(int count) => JsonSerializer.SerializeToUtf8Bytes(Orders(count));

    /// <summary>Many small independent JSON messages (one order each), as on a message queue.</summary>
    public static byte[][] JsonMessages(int count, int seed = 42) =>
        Orders(count, seed).Select(o => JsonSerializer.SerializeToUtf8Bytes(o)).ToArray();

    public static string Text(byte[] utf8) => Encoding.UTF8.GetString(utf8);
}
