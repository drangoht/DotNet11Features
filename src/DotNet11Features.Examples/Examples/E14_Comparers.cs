using System.Collections;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 14 · EqualityComparer<T>.Create(key) and BitArray (span, PopCount) (.NET 11)
// Documentation: docs/en/14-comparers-bitarray.md · docs/fr/14-comparateurs-bitarray.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public record Contact(string Email, string Name, string Source);

// ── Before: a whole class to say "two contacts are equal when their e-mail is" ──────────────────
public sealed class ContactByEmailBefore : IEqualityComparer<Contact>
{
    public bool Equals(Contact? x, Contact? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && StringComparer.OrdinalIgnoreCase.Equals(x.Email, y.Email));

    public int GetHashCode(Contact obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Email);
}

public static class E14_Comparers
{
    static readonly Contact[] Contacts =
    [
        new("anne@example.com", "Anne", "shop"),
        new("ANNE@example.com", "Anne D.", "newsletter"),
        new("paul@example.com", "Paul", "shop"),
        new("Paul@Example.com", "P. Martin", "trade fair"),
        new("lea@example.com", "Léa", "trade fair"),
    ];

    public static void Run()
    {
        Title("14 · EqualityComparer<T>.Create and BitArray (.NET 11)");

        Section("Before: an 8-line IEqualityComparer<Contact> class");
        var uniqueBefore = Contacts.Distinct(new ContactByEmailBefore()).ToList();
        Line(string.Join(", ", uniqueBefore.Select(c => c.Name)));

        Section("After: EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase)");
        var byEmail = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);
        var unique = Contacts.Distinct(byEmail).ToList();
        Line(string.Join(", ", unique.Select(c => c.Name)));
        Check(unique.SequenceEqual(uniqueBefore), "same result, without a dedicated class");

        HashSet<Contact> set = [with(byEmail), .. Contacts];   // with the collection arguments of example 03
        Check(set.Count == 3 && set.Contains(new Contact("LEA@EXAMPLE.COM", "?", "?")),
            "usable everywhere: HashSet, Dictionary, Distinct, GroupBy, Union…");

        Section("BitArray: built from a span, and PopCount to count the bits set to 1");
        // A product options mask returned by a device: 4 bytes = 32 options.
        ReadOnlySpan<byte> mask = [0b1011_0001, 0b0000_1111, 0b1000_0000, 0b0000_0011];

        // Before: a byte[] was required (often a copy), then a loop to count.
        var bitsBefore = new BitArray(mask.ToArray());
        int activeBefore = 0;
        foreach (bool b in bitsBefore) if (b) activeBefore++;
        Line($"before: new BitArray(mask.ToArray()) + foreach loop → {activeBefore} active options");

        // After: straight from the span, with a hardware count (POPCNT instruction).
        var bits = new BitArray(mask);
        Line($"after: new BitArray(mask).PopCount() → {bits.PopCount()} active options");
        Check(bits.PopCount() == activeBefore && activeBefore == 11, "11 bits set out of 32 (4 + 4 + 1 + 2)");
    }
}
