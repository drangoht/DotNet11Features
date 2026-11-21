# 08 · LINQ : `FullJoin` et jointures sans sélecteur (.NET 11)

> **En une phrase** : `catalogue.FullJoin(stock, a => a.Sku, s => s.Sku)` fait enfin la jointure externe complète,
> et toutes les jointures acceptent maintenant d'être appelées **sans sélecteur de résultat** : elles renvoient des
> tuples qu'on déconstruit directement.
>
> Code : [`E08_JointuresLinq.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E08_JointuresLinq.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- jointures`

**Ce qui est nouveau, exactement** (vérifié dans les assemblies de référence de .NET 10 et 11) :

| Méthode | Apparue en |
|---|---|
| `LeftJoin(…, resultSelector)`, `RightJoin(…, resultSelector)` | .NET 10 |
| **`FullJoin`** (avec ou sans sélecteur) | **.NET 11** |
| **`Join`, `GroupJoin`, `LeftJoin`, `RightJoin` sans sélecteur** (renvoient des tuples), avec comparateur optionnel | **.NET 11** |

---

## 1. L'idée expliquée simplement

Une **jointure** associe les éléments de deux listes qui partagent une clé : chaque commande avec son client.

```mermaid
flowchart LR
    subgraph Clients
        D[1 Dupont]
        M[2 Martin]
        U[3 Durand]
    end
    subgraph Commandes
        C1[C-001 → 1]
        C2[C-002 → 1]
        C3[C-003 → 3]
        C4[C-004 → 9]
    end
    D --- C1
    D --- C2
    U --- C3
```

Martin n'a pas de commande, la commande C-004 n'a pas de client (le client 9 n'existe pas). Selon la question posée,
on veut garder ou non ces orphelins :

| Jointure | Garde | Question métier | SQL | LINQ |
|---|---|---|---|---|
| Interne | les paires seulement | « commandes avec leur client » | `INNER JOIN` | `Join` |
| Externe gauche | + clients sans commande | « tous les clients, et leurs commandes s'ils en ont » | `LEFT JOIN` | `LeftJoin` (.NET 10) |
| Externe droite | + commandes sans client | « toutes les commandes, même orphelines » | `RIGHT JOIN` | `RightJoin` (.NET 10) |
| Externe complète | + les deux | « rapprocher deux sources et lister les écarts » | `FULL OUTER JOIN` | **`FullJoin` (.NET 11)** |

---

## 2. Comment on faisait avant

Jusqu'à .NET 9, la jointure externe gauche s'écrivait ainsi :

```csharp
var resultat = Clients
    .GroupJoin(Commandes, c => c.Id, cmd => cmd.ClientId, (client, cmds) => new { client, cmds })
    .SelectMany(x => x.cmds.DefaultIfEmpty(), (x, cmd) => (x.client.Nom, Numero: cmd?.Numero ?? "(aucune)"));
```

.NET 10 a ajouté `LeftJoin` et `RightJoin`, mais **toujours avec un sélecteur de résultat obligatoire** :

```csharp
Clients.LeftJoin(Commandes, c => c.Id, cmd => cmd.ClientId, (client, cmd) => (client, cmd));
//                                                         ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ juste pour faire un tuple
```

Et la jointure **complète** n'existait toujours pas : jointure gauche, puis ajout des orphelins de droite en vérifiant
qu'ils n'ont pas de correspondance.

```csharp
var complet = Catalogue
    .GroupJoin(Stock, a => a.Sku, s => s.Sku, (a, s) => new { a, s })
    .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => (x.a.Sku, Article: (ArticleCatalogue?)x.a, Stock: s))
    .Concat(Stock.Where(s => !Catalogue.Any(a => a.Sku == s.Sku))
                 .Select(s => (s.Sku, Article: (ArticleCatalogue?)null, Stock: (StockEntrepot?)s)));
```

---

## 3. Ce que change .NET 11

```csharp
// FullJoin : les deux côtés peuvent être null
foreach (var (article, stock) in Catalogue.FullJoin(Stock, a => a.Sku, s => s.Sku)) …

// Toutes les jointures sans sélecteur : des tuples (Outer, Inner) à déconstruire
foreach (var (client, cmd) in Clients.LeftJoin(Commandes, c => c.Id, cmd => cmd.ClientId)) …
foreach (var (client, cmd) in Clients.RightJoin(Commandes, c => c.Id, cmd => cmd.ClientId)) …

// Un comparateur de clés en paramètre optionnel
Catalogue.Join(stock, a => a.Sku, s => s.Sku, StringComparer.OrdinalIgnoreCase);
```

Le `FullJoin` se marie très bien avec un `switch` sur le tuple :

```csharp
string diagnostic = (article, stock) switch
{
    (not null, not null) => $"OK, {stock.Quantite} en stock",
    (not null, null)     => "au catalogue mais jamais reçu en entrepôt",
    (null, not null)     => "en entrepôt mais absent du catalogue !",
    (null, null)         => "impossible",
};
```

Ces opérateurs existent pour `Enumerable`, `Queryable` et `AsyncEnumerable`.

### Côte à côte

<table>
<tr><th>Avant</th><th>.NET 11</th></tr>
<tr><td valign="top">

```csharp
Catalogue
  .GroupJoin(Stock,
    a => a.Sku, s => s.Sku,
    (a, s) => new { a, s })
  .SelectMany(
    x => x.s.DefaultIfEmpty(),
    (x, s) => (x.a, s))
  .Concat(Stock
    .Where(s => !Catalogue
      .Any(a => a.Sku == s.Sku))
    .Select(s => (null, s)));
```

</td><td valign="top">

```csharp
Catalogue
  .FullJoin(Stock,
    a => a.Sku, s => s.Sku);







```

</td></tr>
</table>

---

## 4. Résultats

### Sortie de l'exemple

```
   ▸ .NET 11 : Catalogue.FullJoin(Stock, a => a.Sku, s => s.Sku)
     CAFE    OK, 120 en stock
     THE     au catalogue mais jamais reçu en entrepôt
     TASSE   OK, 8 en stock
     FILTRE  en entrepôt mais absent du catalogue !
     ✓ 4 lignes : CAFE, THE, TASSE et FILTRE, comme la version manuelle
```

### Benchmarks (BenchmarkDotNet, 10 000 clients × 30 000 commandes, job court)

| Méthode | Temps | Mémoire |
|---|---:|---:|
| Avant .NET 10 : GroupJoin + SelectMany + DefaultIfEmpty | 2,35 ms | 3,96 Mo |
| .NET 10 : LeftJoin | 2,36 ms | 2,84 Mo (−28 %) |
| Avant : jointure complète manuelle (avec un `HashSet` des clés) | 2,62 ms | 4,48 Mo |
| **.NET 11 : FullJoin** | 4,83 ms (**×1,8**) | 3,49 Mo (−22 %) |

### Comment lire ces chiffres

- **`FullJoin` est plus lent qu'une version manuelle bien écrite** sur ce cas (un tiers des commandes sont
  orphelines). Il reste bien plus simple, alloue moins, et la version manuelle « naïve » de la démo (un `Any()` par
  élément au lieu d'un `HashSet`) serait, elle, quadratique. Sur un chemin critique avec de gros volumes, mesurez.
- **`LeftJoin` vaut l'ancienne incantation en vitesse** et alloue 28 % de moins.
- Avec un fournisseur `IQueryable` (Entity Framework Core…), c'est la base de données qui fait la jointure : la
  question des performances se pose côté SQL, à condition que le fournisseur sache traduire l'opérateur.

---

## 5. Pièges et limites

1. **Le côté manquant vaut `default`** : `null` pour une classe, mais `0` ou `default(T)` pour un struct ou un
   tuple. Pour distinguer « absent » de « valeur nulle », utilisez des types référence ou `Nullable<T>`.
2. **L'ordre** : `LeftJoin` garde l'ordre de la séquence de gauche ; `FullJoin` renvoie d'abord les éléments de
   gauche (associés ou non), puis les orphelins de droite.
3. **Doublons de clé** : comme en SQL, un client avec deux commandes produit deux lignes.

## Sources

- [What's new in .NET 11 libraries — LINQ](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
- [What's new in .NET 10 libraries — LeftJoin et RightJoin](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/libraries)
