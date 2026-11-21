# 03 · Arguments dans les expressions de collection : `[with(...), ...]` (C# 15)

🇬🇧 [English version](../en/03-collection-arguments.md)

> **En une phrase** : `[with(StringComparer.OrdinalIgnoreCase), .. tags]` transmet des arguments au constructeur de
> la collection (comparateur, capacité…) sans quitter la syntaxe `[ ]`.
>
> Code : [`E03_CollectionArguments.cs`](../../src/DotNet11Features.Examples/Examples/E03_CollectionArguments.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- collections`

---

## 1. L'idée expliquée simplement

C# 12 a introduit les **expressions de collection** : `List<int> l = [1, 2, 3];`, `int[] t = [.. a, .. b];`. Une
seule syntaxe pour les tableaux, listes, ensembles, spans…

Mais dès qu'une collection a besoin d'un **réglage à la construction**, cette syntaxe ne suffisait plus :

- un `HashSet<string>` **insensible à la casse** a besoin d'un comparateur ;
- un `Dictionary<string, T>` aussi ;
- une `List<T>` qu'on va remplir de 10 000 éléments gagne à **réserver sa capacité** d'avance.

C# 15 ajoute un élément spécial, `with(...)`, toujours **en premier**, dont les arguments sont passés au
constructeur (ou à la méthode de fabrique) de la collection.

---

## 2. Comment on faisait avant

```csharp
// Un ensemble d'étiquettes, sans doublons de casse
var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var t in Tags) result.Add(t);

// Une liste avec capacité réservée
var list = new List<int>(source.Length * 2);
list.AddRange(source);
```

Ou bien : `new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase)`, qui marche pour ce cas précis mais pas quand
on mélange plusieurs sources et des éléments isolés.

---

## 3. Ce que change C# 15

```csharp
HashSet<string> unique = [with(StringComparer.OrdinalIgnoreCase), .. Tags];

Dictionary<string, decimal> prices = [with(StringComparer.OrdinalIgnoreCase)];

List<int> list = [with(capacity: source.Length * 2), .. source];
```

Les arguments nommés fonctionnent (`capacity:`), et `with(...)` se combine avec tout le reste : éléments isolés,
`..spread`, et même un comparateur créé avec [`EqualityComparer<T>.Create`](14-comparateurs-bitarray.md) :

```csharp
HashSet<Contact> set = [with(byEmail), .. Contacts];
```

### Côte à côte

<table>
<tr><th>Avant</th><th>C# 15</th></tr>
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

## 4. Sortie de l'exemple

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

Sans capacité, la liste commence avec 4 cases et **double** à chaque fois qu'elle est pleine : 13 tableaux sont
alloués puis jetés, et chaque élément est recopié plusieurs fois. Avec `with(capacity: 10_000)`, un seul tableau de
40 000 octets : **3,3 fois moins de mémoire**, et aucune recopie.

La capacité n'est pas une nouveauté de .NET 11, mais `with(capacity: …)` la rend enfin accessible depuis la
syntaxe `[ ]` : on n'a plus à choisir entre lisibilité et allocations.

---

## 5. Pièges et limites

1. **`with(...)` doit être le premier élément.** Ailleurs dans la liste, c'est une erreur de compilation.
2. **Pas de `with(...)` pour les tableaux** (`int[]`) ni pour les spans : ils n'ont pas de constructeur à paramètres
   utiles.
3. **Pour `[.. source]` seul**, le compilateur réserve souvent déjà la bonne taille quand il connaît la longueur de
   la source. `with(capacity:)` est utile quand on va **ajouter des éléments ensuite**, comme dans la mesure.
4. Les collections maison sont prises en charge si elles exposent un constructeur ou une méthode de fabrique
   `[CollectionBuilder]` qui accepte ces arguments.

## Sources

- [What's new in C# 15 — Collection expression arguments](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
