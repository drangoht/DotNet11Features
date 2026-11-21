# 03 · Arguments in collection expressions: `[with(...), ...]` (C# 15)

🇫🇷 [Version française](../fr/03-arguments-collection.md)

> **In one sentence**: `[with(StringComparer.OrdinalIgnoreCase), .. tags]` passes arguments to the collection's
> constructor (comparer, capacity…) without leaving the `[ ]` syntax.
>
> Code: [`E03_CollectionArguments.cs`](../../src/DotNet11Features.Examples/Examples/E03_CollectionArguments.cs) ·
> Run: `dotnet run -c Release --project src/DotNet11Features.Examples -- collections`

---

## 1. The idea, simply

C# 12 introduced **collection expressions**: `List<int> l = [1, 2, 3];`, `int[] t = [.. a, .. b];`. One syntax for
arrays, lists, sets, spans…

But as soon as a collection needs a **setting at construction time**, that syntax was no longer enough:

- a **case-insensitive** `HashSet<string>` needs a comparer;
- so does a `Dictionary<string, T>`;
- a `List<T>` that will receive 10,000 items benefits from **reserving its capacity** up front.

C# 15 adds a special element, `with(...)`, always **first**, whose arguments are passed to the collection's
constructor (or factory method).

---

## 2. How we did it before

```csharp
// A set of tags, without case duplicates
var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var t in Tags) result.Add(t);

// A list with reserved capacity
var list = new List<int>(source.Length * 2);
list.AddRange(source);
```

Or `new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase)`, which works for this specific case but not when
you mix several sources and single items.

---

## 3. What C# 15 changes

```csharp
HashSet<string> unique = [with(StringComparer.OrdinalIgnoreCase), .. Tags];

Dictionary<string, decimal> prices = [with(StringComparer.OrdinalIgnoreCase)];

List<int> list = [with(capacity: source.Length * 2), .. source];
```

Named arguments work (`capacity:`), and `with(...)` combines with everything else: single items, `..spread`, and
even a comparer built with [`EqualityComparer<T>.Create`](14-comparers-bitarray.md):

```csharp
HashSet<Contact> set = [with(byEmail), .. Contacts];
```

### Side by side

<table>
<tr><th>Before</th><th>C# 15</th></tr>
<tr><td valign="top">

```csharp
var unique = new HashSet<string>(
  StringComparer.OrdinalIgnoreCase);
foreach (var t in Tags)
  unique.Add(t);
unique.Add("New");
```

</td><td valign="top">

```csharp
HashSet<string> unique =
[
  with(StringComparer.OrdinalIgnoreCase),
  .. Tags,
  "New",
];
```

</td></tr>
</table>

---

## 4. Output of the example

```
── 03 · Arguments in collection expressions (C# 15) ──
   ▸ After: [with(StringComparer.OrdinalIgnoreCase), .. Tags]
     Promo, New, Sale
     ✓ 6 tags → 3 unique ones, ignoring case
   ▸ Case-insensitive dictionary
     ✓ prices["coffee-250g"] finds the "COFFEE-250G" key
   ▸ Capacity reserved up front: fewer reallocations
     ✓ [with(capacity: 2000), .. source] → Capacity = 2000
     10,000 Add without capacity: 131,400 bytes allocated (13 successive arrays: 4, 8, 16… 16,384 slots)
     10,000 Add with capacity:     40,056 bytes allocated (a single array)
     ✓ reserving the capacity cuts allocations by more than half
```

Without a capacity, the list starts with 4 slots and **doubles** every time it is full: 13 arrays are allocated and
then thrown away, and every item is copied several times. With `with(capacity: 10_000)`, a single 40,000-byte array:
**3.3 times less memory**, and no copying.

Capacity is not new in .NET 11, but `with(capacity: …)` finally makes it reachable from the `[ ]` syntax: you no
longer have to choose between readability and allocations.

---

## 5. Pitfalls and limits

1. **`with(...)` must be the first element.** Anywhere else in the list, it is a compile error.
2. **No `with(...)` for arrays** (`int[]`) or spans: they have no useful constructor parameters.
3. **For `[.. source]` alone**, the compiler often already reserves the right size when it knows the source length.
   `with(capacity:)` is useful when you **add items afterwards**, as in the measurement.
4. Custom collections are supported if they expose a constructor or a `[CollectionBuilder]` factory method that
   accepts these arguments.

## Sources

- [What's new in C# 15 — Collection expression arguments](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
