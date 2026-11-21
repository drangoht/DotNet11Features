# 04 · Extension indexers (C# 15)

🇫🇷 [Version française](../fr/04-indexeurs-extension.md)

> **In one sentence**: an `extension(JsonElement e) { public JsonElement this[string name] => … }` block adds the
> `root["order"]["customer"]` syntax to a type you do not own.
>
> Code: [`E04_ExtensionIndexers.cs`](../../src/DotNet11Features.Examples/Examples/E04_ExtensionIndexers.cs) ·
> Run: `dotnet run -c Release --project src/DotNet11Features.Examples -- indexers`

---

## 1. The idea, simply

**Extension methods** (C# 3, 2007) let you add methods to a type you cannot change: `"text".ToShouting()`. C# 14
(.NET 10) generalized the idea with **`extension` blocks**, which also accept properties and static members.

**Indexers** were missing, that is the `object[key]` syntax. C# 15 adds them.

The typical example: `JsonElement` (System.Text.Json, read-only and very fast) has **no** indexer by property name.
To go down three levels, you chain `GetProperty`.

---

## 2. How we did it before

```csharp
// Directly
root.GetProperty("order").GetProperty("customer").GetProperty("city").GetString();

// With a home-made extension method, a bit shorter
public static JsonElement Prop(this JsonElement element, string name) => element.GetProperty(name);
root.Prop("order").Prop("customer").Prop("city").GetString();
```

It works, but you lose the natural syntax of a lookup by key, and a method cannot be used for **assignment**
(`x[key] = value`) when the type would allow it.

---

## 3. What C# 15 changes

```csharp
public static class IndexerExtensions
{
    extension(JsonElement element)
    {
        public JsonElement this[string name] => element.GetProperty(name);
    }

    extension(IReadOnlyList<LineItem> lines)
    {
        public LineItem this[string sku] => lines.First(l => l.Sku == sku);
    }

    extension<T>(IEnumerable<T> source)          // generic, with Index (^1 = the last one)
    {
        public T this[Index position] => source.ElementAt(position);
    }
}

string city = root["order"]["customer"]["city"].GetString();
LineItem tea = lines["GREEN-TEA"];
int last = numbers[^1];                           // on a LINQ sequence, without .ToList()
```

### Side by side

<table>
<tr><th>Before: extension method</th><th>C# 15: extension indexer</th></tr>
<tr><td valign="top">

```csharp
public static JsonElement Prop(
  this JsonElement e, string name)
  => e.GetProperty(name);

root.Prop("order")
    .Prop("customer")
    .Prop("city")
```

</td><td valign="top">

```csharp
extension(JsonElement e)
{
  public JsonElement this[string name]
    => e.GetProperty(name);
}

root["order"]["customer"]["city"]

```

</td></tr>
</table>

The generated code is the same in both cases: a static method call. It is a **readability** improvement, with no
runtime cost.

---

## 4. Output of the example

```
── 04 · Extension indexers (C# 15) ──────────────────────────
   ▸ After: root["order"]["customer"]["city"]
     Lyon
     ✓ the extension indexer chains like a built-in indexer
   ▸ A business indexer on IReadOnlyList<LineItem>
     lines["GREEN-TEA"] → Green tea × 1
     ✓ lookup by product code with the [ ] syntax
     ✓ the built-in lines[0] indexer still wins and is unchanged
   ▸ A generic indexer on IEnumerable<T>, with ^1 (from the end)
     numbers = 3, 6, 9 ; numbers[^1] = 9
     ✓ ^1 on a LINQ sequence without .ToList()
```

---

## 5. Pitfalls and limits

1. **The type's own members always win**: `lines[0]` calls the list's built-in indexer, not the extension. An
   extension can only add overloads the type does not have (here, an indexer by `string`).
2. **The block parameter must be named** (`extension(JsonElement element)`, not `extension(JsonElement)`): an indexer
   is always an instance member, it needs a receiver.
3. **Beware of hidden complexity**: `source[^1]` on an `IEnumerable<T>` walks the whole sequence, and
   `lines["GREEN-TEA"]` is a linear search. The `[ ]` syntax suggests direct access: keep it for cases where the cost
   stays reasonable, or document it.

## Sources

- [What's new in C# 15 — Extension indexers](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
