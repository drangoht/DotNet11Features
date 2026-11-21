using System.Text.Json;
using System.Text.Json.Serialization;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 10 · System.Text.Json: unions, closed hierarchies, per-type naming policy (.NET 11)
// Documentation: docs/en/10-json-unions.md · docs/fr/10-json-unions.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// ── A payment provider response: two object shapes, with no "type" field ─────────────────────────
public record PaymentAccepted(string Authorization, decimal Amount);
public record PaymentDeclined(string Reason, int ErrorCode);

// After: a union + the structural classifier, which picks the case from the properties present.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union PaymentResponse(PaymentAccepted, PaymentDeclined);

// A union of simple values: the JSON token kind (number, string) is enough to pick the case.
public union Reading(double, string);

// Before: a hand-written converter that inspects the JSON to guess the type.
public sealed class PaymentResponseConverterBefore : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.TryGetProperty("Authorization", out _)) return doc.RootElement.Deserialize<PaymentAccepted>(options)!;
        if (doc.RootElement.TryGetProperty("Reason", out _)) return doc.RootElement.Deserialize<PaymentDeclined>(options)!;
        throw new JsonException("Unknown payment response");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

// ── Closed hierarchy: polymorphism is inferred, no need to list every derived type ─────────────
// Before: one [JsonDerivedType] per derived class, maintained by hand.
[JsonPolymorphic]
[JsonDerivedType(typeof(PercentDiscountBefore), "PercentDiscountBefore")]
[JsonDerivedType(typeof(AmountDiscountBefore), "AmountDiscountBefore")]
public abstract record DiscountBefore;
public record PercentDiscountBefore(int Percent) : DiscountBefore;
public record AmountDiscountBefore(decimal Amount) : DiscountBefore;

// After: closed + InferClosedTypePolymorphism. Adding a derived type needs nothing more.
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record Discount;
public record PercentDiscount(int Percent) : Discount;
public record AmountDiscount(decimal Amount) : Discount;

// ── Naming policy carried by the type ───────────────────────────────────────────────────────────
// Before: a [JsonPropertyName("snake_case")] on every property of that single type.
public record WebhookBefore(
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("amount_cents")] long AmountCents);

// After: a single attribute on the type; the rest of the application keeps its global policy.
[JsonNamingPolicy(JsonKnownNamingPolicy.SnakeCaseLower)]
public record Webhook(string EventId, DateTimeOffset CreatedAt, long AmountCents);

public static class E10_JsonUnions
{
    public static void Run()
    {
        Title("10 · System.Text.Json: unions and closed hierarchies (.NET 11)");

        Section("Union of simple values: written as is, with no wrapper and no $type");
        Reading[] readings = [21.5, "sensor offline"];
        string readingsJson = JsonSerializer.Serialize(readings);
        Line(readingsJson);
        Check(readingsJson == """[21.5,"sensor offline"]""", "only the value of the active case is written");
        var readBack = JsonSerializer.Deserialize<Reading[]>(readingsJson)!;
        Check(readBack[0].Value is double && readBack[1].Value is string, "when reading, a number gives the double case and a string the string case");

        Section("Before: a hand-written JsonConverter to guess the shape of the response");
        var optionsBefore = new JsonSerializerOptions { Converters = { new PaymentResponseConverterBefore() } };
        object old = JsonSerializer.Deserialize<object>("""{"Reason":"Insufficient funds","ErrorCode":51}""", optionsBefore)!;
        Line($"{old.GetType().Name}: {old}");

        Section("After: [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]");
        string[] responses = ["""{"Authorization":"A1B2C3","Amount":42.5}""", """{"Reason":"Insufficient funds","ErrorCode":51}"""];
        foreach (string json in responses)
        {
            PaymentResponse response = JsonSerializer.Deserialize<PaymentResponse>(json);
            string text = response switch
            {
                PaymentAccepted a => $"accepted, authorization {a.Authorization} for {a.Amount} €",
                PaymentDeclined d => $"declined ({d.ErrorCode}): {d.Reason}",
            };
            Line($"{json,-55} → {text}");
        }
        Check(JsonSerializer.Deserialize<PaymentResponse>(responses[1]).Value is PaymentDeclined, "the case is picked from the property names present");
        Check(JsonSerializer.Serialize<PaymentResponse>(new PaymentAccepted("A1B2C3", 42.5m)) == responses[0], "when writing, the active case object is written without a wrapper");
        try
        {
            JsonSerializer.Deserialize<PaymentResponse>("""{"Unknown":true}""");
            Check(false, "an object that looks like no case should have been rejected");
        }
        catch (JsonException) { Check(true, "an object that matches no case throws JsonException"); }

        Section("Closed hierarchy: InferClosedTypePolymorphism replaces the list of [JsonDerivedType]");
        DiscountBefore[] discountsBefore = [new PercentDiscountBefore(10), new AmountDiscountBefore(5m)];
        Discount[] discounts = [new PercentDiscount(10), new AmountDiscount(5m)];
        Line($"before: {JsonSerializer.Serialize(discountsBefore)}");
        string discountsJson = JsonSerializer.Serialize(discounts);
        Line($"after:  {discountsJson}");
        var back = JsonSerializer.Deserialize<Discount[]>(discountsJson)!;
        Check(back[0] is PercentDiscount { Percent: 10 } && back[1] is AmountDiscount { Amount: 5m }, "full round trip, without a single [JsonDerivedType]");

        Section("Type-level naming policy: [JsonNamingPolicy(SnakeCaseLower)]");
        var date = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        string before = JsonSerializer.Serialize(new WebhookBefore("evt_42", date, 4250));
        string after = JsonSerializer.Serialize(new Webhook("evt_42", date, 4250));
        Line($"before (3 attributes): {before}");
        Line($"after (1 attribute):   {after}");
        Check(before == after, "same JSON, one attribute instead of one per property");

        Section("And the new global JsonNamingPolicy.PascalCase");
        var pascal = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.PascalCase };
        string pascalJson = JsonSerializer.Serialize(new { orderNumber = "O-001", totalAmount = 42.5 }, pascal);
        Line(pascalJson);
        Check(pascalJson == """{"OrderNumber":"O-001","TotalAmount":42.5}""", "camelCase properties written as PascalCase");
    }
}
