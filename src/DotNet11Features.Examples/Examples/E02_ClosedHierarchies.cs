using System.Diagnostics;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 02 · Closed hierarchies (C# 15): "these derived classes, and no others"
// Documentation: docs/en/02-closed-hierarchies.md · docs/fr/02-hierarchies-fermees.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// ── Before: an abstract class. Anyone, anywhere, can derive from it. ──────────────────────────
public abstract record OrderStateBefore;
public record DraftBefore : OrderStateBefore;
public record PaidBefore(DateOnly On) : OrderStateBefore;
public record ShippedBefore(string TrackingNumber) : OrderStateBefore;

// ── After (.NET 11): the closed keyword. Only this assembly can declare direct derived types. ──
public closed record OrderState;
public record Draft : OrderState;
public record Paid(DateOnly On) : OrderState;
public record Shipped(string TrackingNumber) : OrderState;

public static class E02_ClosedHierarchies
{
    // Before: the compiler cannot know there are only three states.
    // Without the last arm it reports CS8509; with it, a logic error becomes a runtime exception.
    static string LabelBefore(OrderStateBefore state) => state switch
    {
        DraftBefore => "draft",
        PaidBefore p => $"paid on {p.On:MM/dd}",
        ShippedBefore s => $"shipped ({s.TrackingNumber})",
        _ => throw new UnreachableException($"Unknown state: {state.GetType().Name}"),
    };

    // After: three arms, that is complete. Adding a "Delivered" state in this file breaks the build
    // here (CS8509 as an error), instead of throwing UnreachableException in production.
    static string Label(OrderState state) => state switch
    {
        Draft => "draft",
        Paid p => $"paid on {p.On:MM/dd}",
        Shipped s => $"shipped ({s.TrackingNumber})",
    };

    // Positional deconstruction of records works as usual.
    static bool CanBeCancelled(OrderState state) => state switch
    {
        Draft or Paid => true,
        Shipped(_) => false,
    };

    public static void Run()
    {
        Title("02 · Closed hierarchies (C# 15)");

        Section("Before: abstract record + a \"_ => throw\" arm");
        OrderStateBefore[] oldStates = [new DraftBefore(), new PaidBefore(new DateOnly(2026, 11, 10)), new ShippedBefore("PARCEL-42")];
        foreach (var state in oldStates) Line(LabelBefore(state));

        Section("After: public closed record OrderState;");
        OrderState[] states = [new Draft(), new Paid(new DateOnly(2026, 11, 10)), new Shipped("PARCEL-42")];
        foreach (var state in states) Line($"{Label(state),-24} cancellable: {(CanBeCancelled(state) ? "yes" : "no")}");

        Section("What the compiler really generates");
        Type t = typeof(OrderState);
        var attribute = t.GetCustomAttributesData().Single(a => a.AttributeType.Name == "IsClosedTypeAttribute");
        var derived = (IEnumerable<System.Reflection.CustomAttributeTypedArgument>)attribute.NamedArguments.Single().TypedValue.Value!;
        Line($"[IsClosedType(DerivedTypes = {string.Join(", ", derived.Select(d => ((Type)d.Value!).Name))})]");
        Check(t.IsAbstract, "a closed class is implicitly abstract (new OrderState() is not allowed)");
        Check(derived.Count() == 3, "the list of direct derived types is written into the assembly");
    }
}
