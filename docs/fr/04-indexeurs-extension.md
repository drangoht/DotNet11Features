# 04 · Indexeurs d'extension (C# 15)

🇬🇧 [English version](../en/04-extension-indexers.md)

> **En une phrase** : un bloc `extension(JsonElement e) { public JsonElement this[string name] => … }` ajoute la
> syntaxe `root["order"]["customer"]` à un type qu'on ne possède pas.
>
> Code : [`E04_ExtensionIndexers.cs`](../../src/DotNet11Features.Examples/Examples/E04_ExtensionIndexers.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- indexers`

---

## 1. L'idée expliquée simplement

Les **méthodes d'extension** (C# 3, 2007) permettent d'ajouter des méthodes à un type qu'on ne peut pas modifier :
`"texte".EnMajuscules()`. C# 14 (.NET 10) a généralisé l'idée avec les **blocs `extension`**, qui acceptent aussi des
propriétés et des membres statiques.

Il manquait les **indexeurs**, c'est-à-dire la syntaxe `objet[clé]`. C# 15 les ajoute.

L'exemple type : `JsonElement` (System.Text.Json, lecture seule et très rapide) n'a **pas** d'indexeur par nom de
propriété. Pour descendre de trois niveaux, il faut enchaîner `GetProperty`.

---

## 2. Comment on faisait avant

```csharp
// Directement
root.GetProperty("order").GetProperty("customer").GetProperty("city").GetString();

// Avec une méthode d'extension maison, un peu plus courte
public static JsonElement Prop(this JsonElement element, string name) => element.GetProperty(name);
root.Prop("order").Prop("customer").Prop("city").GetString();
```

Ça marche, mais on perd la syntaxe naturelle d'un accès par clé, et une méthode ne peut pas servir à
l'**affectation** (`x[clé] = valeur`) quand le type le permettrait.

---

## 3. Ce que change C# 15

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

    extension<T>(IEnumerable<T> source)          // générique, avec Index (^1 = le dernier)
    {
        public T this[Index position] => source.ElementAt(position);
    }
}

string city = root["order"]["customer"]["city"].GetString();
LineItem tea = lines["GREEN-TEA"];
int last = numbers[^1];                           // sur une séquence LINQ, sans .ToList()
```

### Côte à côte

<table>
<tr><th>Avant : méthode d'extension</th><th>C# 15 : indexeur d'extension</th></tr>
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

Le code généré est le même dans les deux cas : un appel de méthode statique. C'est une amélioration de
**lisibilité**, sans coût à l'exécution.

---

## 4. Sortie de l'exemple

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

## 5. Pièges et limites

1. **Les membres du type gagnent toujours** : `lines[0]` appelle l'indexeur natif de la liste, pas l'extension.
   Une extension ne peut qu'ajouter des surcharges que le type n'a pas (ici, un indexeur par `string`).
2. **Le paramètre du bloc doit être nommé** (`extension(JsonElement element)`, pas `extension(JsonElement)`) : un
   indexeur est toujours un membre d'instance, il lui faut un récepteur.
3. **Attention à la complexité cachée** : `source[^1]` sur un `IEnumerable<T>` parcourt toute la séquence, et
   `lines["GREEN-TEA"]` fait une recherche linéaire. La syntaxe `[ ]` suggère un accès direct : réservez-la aux
   cas où le coût reste raisonnable, ou documentez-le.

## Sources

- [What's new in C# 15 — Extension indexers](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
