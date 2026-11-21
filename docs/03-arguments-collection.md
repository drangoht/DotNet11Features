# 03 · Arguments dans les expressions de collection : `[with(...), ...]` (C# 15)

> **En une phrase** : `[with(StringComparer.OrdinalIgnoreCase), .. etiquettes]` transmet des arguments au
> constructeur de la collection (comparateur, capacité…) sans quitter la syntaxe `[ ]`.
>
> Code : [`E03_ArgumentsCollection.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E03_ArgumentsCollection.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- collections`

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
var resultat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var e in Etiquettes) resultat.Add(e);

// Une liste avec capacité réservée
var liste = new List<int>(source.Length * 2);
liste.AddRange(source);
```

Ou bien : `new HashSet<string>(Etiquettes, StringComparer.OrdinalIgnoreCase)`, qui marche pour ce cas précis mais
pas quand on mélange plusieurs sources et des éléments isolés.

---

## 3. Ce que change C# 15

```csharp
HashSet<string> uniques = [with(StringComparer.OrdinalIgnoreCase), .. Etiquettes];

Dictionary<string, decimal> prix = [with(StringComparer.OrdinalIgnoreCase)];

List<int> liste = [with(capacity: source.Length * 2), .. source];
```

Les arguments nommés fonctionnent (`capacity:`), et `with(...)` se combine avec tout le reste : éléments isolés,
`..spread`, et même un comparateur créé avec [`EqualityComparer<T>.Create`](14-comparateurs-bitarray.md) :

```csharp
HashSet<Contact> ensemble = [with(parEmail), .. Contacts];
```

### Côte à côte

<table>
<tr><th>Avant</th><th>C# 15</th></tr>
<tr><td valign="top">

```csharp
var uniques = new HashSet<string>(
  StringComparer.OrdinalIgnoreCase);
foreach (var e in Etiquettes)
  uniques.Add(e);
uniques.Add("Nouveau");
```

</td><td valign="top">

```csharp
HashSet<string> uniques =
[
  with(StringComparer.OrdinalIgnoreCase),
  .. Etiquettes,
  "Nouveau",
];
```

</td></tr>
</table>

---

## 4. Résultats de l'exemple

```
── 03 · Arguments dans les expressions de collection (C# 15) ──
   ▸ Après : [with(StringComparer.OrdinalIgnoreCase), .. Etiquettes]
     Promo, Nouveau, Soldes
     ✓ 6 étiquettes → 3 uniques sans tenir compte de la casse
   ▸ Dictionnaire insensible à la casse
     ✓ prix["cafe-250g"] retrouve la clé "CAFE-250G"
   ▸ Capacité réservée d'avance : moins de réallocations
     ✓ [with(capacity: 2000), .. source] → Capacity = 2000
     10 000 Add sans capacité : 131 400 octets alloués (13 tableaux successifs : 4, 8, 16… 16 384 cases)
     10 000 Add avec capacité :  40 056 octets alloués (un seul tableau)
     ✓ réserver la capacité divise les allocations par plus de 2
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
