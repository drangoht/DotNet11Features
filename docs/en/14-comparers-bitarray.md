# 14 · `EqualityComparer<T>.Create(key)` and `BitArray` (.NET 11)

🇫🇷 [Version française](../fr/14-comparateurs-bitarray.md)

> **In one sentence**: `EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase)` replaces a
> whole `IEqualityComparer<T>` class, and `BitArray` can be built from a span and count its bits with `PopCount()`.
>
> Code: [`E14_Comparers.cs`](../../src/DotNet11Features.Examples/Examples/E14_Comparers.cs) ·
> Run: `dotnet run -c Release --project src/DotNet11Features.Examples -- comparers`

---

## 1. `EqualityComparer<T>.Create` with a key selector

### The idea, simply

Two contact records `anne@example.com` and `ANNE@example.com` are the same person. To remove duplicates, you have to
tell .NET **what "equal" means** for a contact: "same e-mail, ignoring case".

An equality comparer must provide **two** consistent things: `Equals` (are they equal?) and `GetHashCode` (which
drawer to store the object in, to find it again quickly). Forgetting one or making them inconsistent is a classic
source of bugs: a `HashSet` that accepts duplicates.

### Before

```csharp
public sealed class ContactByEmailBefore : IEqualityComparer<Contact>
{
    public bool Equals(Contact? x, Contact? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && StringComparer.OrdinalIgnoreCase.Equals(x.Email, y.Email));

    public int GetHashCode(Contact obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Email);
}
```

.NET 8 had added `EqualityComparer<T>.Create(equals, getHashCode)`, but you still wrote both functions and kept them
consistent yourself.

### .NET 11

```csharp
var byEmail = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);

Contacts.Distinct(byEmail);
HashSet<Contact> set = [with(byEmail), .. Contacts];      // with the collection arguments (example 03)
new Dictionary<Contact, int>(byEmail);
```

You give **the key** and, optionally, the comparer for that key; `Equals` and `GetHashCode` are derived from it,
always consistent.

### Benchmark: `Distinct` over 10,000 contacts (4,000 unique)

| Method | Time | Memory |
|---|---:|---:|
| Hand-written `IEqualityComparer<Contact>` class | 266 µs | 197.6 KB |
| `EqualityComparer<Contact>.Create(c => c?.Email, …)` | 270 µs | 197.6 KB |

No significant difference: the delegate call costs a few nanoseconds, drowned in hashing the string. You save a
class, not performance.

---

## 2. `BitArray`: built from a span, and `PopCount`

A device returns a mask of 32 options in 4 bytes; we want to know how many are on.

<table>
<tr><th>Before</th><th>.NET 11</th></tr>
<tr><td valign="top">

```csharp
// BitArray only accepted a byte[]:
// a copy if the bytes are in a span
var bits = new BitArray(mask.ToArray());
int active = 0;
foreach (bool b in bits)
  if (b) active++;
```

</td><td valign="top">

```csharp
var bits = new BitArray(mask);
int active = bits.PopCount();



```

</td></tr>
</table>

`PopCount` uses the `POPCNT` CPU instruction (counts the bits set over 64 bits at once), instead of a bit-by-bit loop
that boxes a boolean on every iteration of the non-generic `foreach`. Constructors added: `ReadOnlySpan<byte>`,
`ReadOnlySpan<bool>`, `ReadOnlySpan<int>`.

---

## 3. Output of the example

```
   ▸ Before: an 8-line IEqualityComparer<Contact> class
     Anne, Paul, Léa
   ▸ After: EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase)
     Anne, Paul, Léa
     ✓ same result, without a dedicated class
     ✓ usable everywhere: HashSet, Dictionary, Distinct, GroupBy, Union…
   ▸ BitArray: built from a span, and PopCount to count the bits set to 1
     before: new BitArray(mask.ToArray()) + foreach loop → 11 active options
     after: new BitArray(mask).PopCount() → 11 active options
     ✓ 11 bits set out of 32 (4 + 4 + 1 + 2)
```

## 4. Pitfalls and limits

1. **The selector receives `T?`**: write `c => c?.Email`, not `c => c.Email`, or the compiler reports CS8602
   (possible dereference of `null`). Hit while writing the example.
2. **The key must be stable**: if a contact's e-mail changes while it sits in a `HashSet`, it can no longer be found
   (already true with a home-made class).

## Sources

- [What's new in .NET 11 libraries — Collections](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
