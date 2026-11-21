using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 01 · Unions (C# 15): "a value that is exactly one of these types"
// Documentation: docs/en/01-unions.md · docs/fr/01-unions.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// Types that already exist and have NOTHING in common (no shared base class).
public record Card(string Last4Digits);
public record BankTransfer(string Iban);
public record Voucher(string Code, decimal Balance);

// ── After (.NET 11): one line ───────────────────────────────────────────────────────────────────
public union PaymentMethod(Card, BankTransfer, Voucher);

// A union can even group types you do not own: int, Guid, string…
public union CustomerId(int, Guid, string);

// ── Before: the most common hand-written version, a wrapper class ───────────────────────────────
public sealed class PaymentMethodBefore
{
    private readonly object _value;
    private PaymentMethodBefore(object value) => _value = value;

    public static implicit operator PaymentMethodBefore(Card c) => new(c);
    public static implicit operator PaymentMethodBefore(BankTransfer t) => new(t);
    public static implicit operator PaymentMethodBefore(Voucher v) => new(v);

    // The compiler does not know that _value can only be one of the three types:
    // every caller needs a "default" case that should never happen.
    public T Match<T>(Func<Card, T> card, Func<BankTransfer, T> transfer, Func<Voucher, T> voucher) => _value switch
    {
        Card c => card(c),
        BankTransfer t => transfer(t),
        Voucher v => voucher(v),
        _ => throw new InvalidOperationException("Impossible case… until someone adds a type."),
    };
}

public static class E01_Unions
{
    // Before: go through Match(…) and its three lambdas, or switch on an object.
    static string DescribeBefore(PaymentMethodBefore method) => method.Match(
        card: c => $"card ending in {c.Last4Digits}",
        transfer: t => $"transfer from {t.Iban[..4]}…",
        voucher: v => $"voucher {v.Code} ({v.Balance} €)");

    // After: an ordinary switch. No "_ =>": the compiler knows the list is complete.
    // Add a 4th type to the union and this method no longer compiles (CS8509, turned into an error in
    // Directory.Build.props): you cannot forget a place.
    static string Describe(PaymentMethod method) => method switch
    {
        Card c => $"card ending in {c.Last4Digits}",
        BankTransfer t => $"transfer from {t.Iban[..4]}…",
        Voucher v => $"voucher {v.Code} ({v.Balance} €)",
    };

    static string Normalize(CustomerId id) => id switch
    {
        int number => $"C-{number:D6}",
        Guid guid => $"G-{guid.ToString()[..8]}",
        string code => code.ToUpperInvariant(),
    };

    public static void Run()
    {
        Title("01 · Unions (C# 15)");

        Section("Before: a hand-written wrapper class");
        PaymentMethodBefore old = new Card("4242");
        Line(DescribeBefore(old));

        Section("After: public union PaymentMethod(Card, BankTransfer, Voucher);");
        PaymentMethod[] methods = [new Card("4242"), new BankTransfer("FR7630006000011234567890189"), new Voucher("XMAS26", 25m)];
        foreach (PaymentMethod method in methods) Line(Describe(method));

        Section("A union of types you do not own: int, Guid, string");
        CustomerId[] ids = [1234, Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), "vip-smith"];
        foreach (CustomerId id in ids) Line($"{id.Value,-38} → {Normalize(id)}");

        Section("What the compiler really generates");
        Type t = typeof(PaymentMethod);
        Line($"PaymentMethod is a {(t.IsValueType ? "struct" : "class")} implementing {string.Join(", ", t.GetInterfaces().Select(i => i.Name))}");
        Line($"Constructors: {string.Join(", ", t.GetConstructors().Select(c => c.GetParameters()[0].ParameterType.Name))}");
        Line($"Attributes: {string.Join(", ", t.GetCustomAttributesData().Select(a => a.AttributeType.Name).Where(n => !n.StartsWith("Nullable")))}");
        Check(t.IsValueType, "a union is a struct (the wrapper itself is not allocated)");
        Check(t.GetProperty("Value")?.PropertyType == typeof(object), "the active case is stored in a Value property of type object");

        Section("The hidden cost: a value type is boxed into Value");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { CustomerId id = i; _ = id.Value; }
        long bytesPerInt = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        before = GC.GetAllocatedBytesForCurrentThread();
        string code = "vip";
        for (int i = 0; i < 1000; i++) { CustomerId id = code; _ = id.Value; }
        long bytesPerString = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        Line($"CustomerId id = 42;    → {bytesPerInt} bytes allocated (the int is boxed)");
        Line($"CustomerId id = \"vip\"; → {bytesPerString} byte allocated (a reference is just copied)");
        Check(bytesPerInt == 24 && bytesPerString == 0, "boxed int = 24 bytes, string = 0 byte");

        Section("The default value of a union has no active case");
        PaymentMethod empty = default;
        Check(empty.Value is null, "default(PaymentMethod).Value is null");
        try { Describe(empty); Check(false, "the switch should have failed"); }
        catch (System.Runtime.CompilerServices.SwitchExpressionException)
        {
            Check(true, "a switch on an empty union throws SwitchExpressionException (the compiler warns: CS8655)");
        }
    }
}
