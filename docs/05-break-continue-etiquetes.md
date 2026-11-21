# 05 · `break` et `continue` étiquetés (C# 15)

> **En une phrase** : `allees: for (…) { for (…) { … break allees; } }` sort directement de la boucle extérieure,
> sans variable « trouvé » ni `goto`.
>
> Code : [`E05_BoucleEtiquetee.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E05_BoucleEtiquetee.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- boucles`

---

## 1. L'idée expliquée simplement

Dans un entrepôt de 4 allées × 5 emplacements, on cherche le **premier emplacement vide**. Deux boucles imbriquées :
les allées, puis les emplacements de chaque allée.

Quand on trouve, on veut **tout arrêter**. Mais `break` ne sort que de la boucle **la plus proche** : on se retrouve
dans la boucle des allées, qui passe tranquillement à l'allée suivante.

C# 15 permet de **nommer une boucle** et de dire de laquelle on sort. Java, JavaScript, Kotlin, Rust, Go et Swift
le font depuis longtemps.

---

## 2. Comment on faisait avant

**Solution 1 : un drapeau**, testé à chaque niveau.

```csharp
bool trouve = false;
for (int allee = 0; allee < Entrepot.Length; allee++)
{
    for (int place = 0; place < Entrepot[allee].Length; place++)
    {
        if (Entrepot[allee][place] == 0) { resultat = (allee, place); trouve = true; break; }
    }
    if (trouve) break;   // à ne pas oublier, sinon on continue à chercher
}
```

**Solution 2 : `goto`**, direct mais mal vu (et l'analyseur IDE0410 signale désormais ce motif).

```csharp
for (…)
    for (…)
        if (Entrepot[allee][place] == 0) { resultat = (allee, place); goto Fin; }
Fin:
```

**Solution 3 : extraire une méthode** et faire `return`. Propre, mais on découpe le code juste pour contourner une
limite du langage.

---

## 3. Ce que change C# 15

```csharp
allees: for (int allee = 0; allee < Entrepot.Length; allee++)
{
    for (int place = 0; place < Entrepot[allee].Length; place++)
    {
        if (Entrepot[allee][place] == 0) { resultat = (allee, place); break allees; }
    }
}
```

Et `continue` étiqueté, pour passer à l'itération suivante de la boucle extérieure :

```csharp
allees: foreach (var (allee, index) in Entrepot.Select((a, i) => (a, i)))
{
    foreach (int stock in allee)
    {
        if (stock == 0) continue allees;   // inutile de regarder le reste de cette allée
    }
    completes.Add(index);                  // atteint seulement si aucune rupture
}
```

```mermaid
flowchart TB
    A["allees: for allée"] --> P["for emplacement"]
    P -- "stock > 0" --> P
    P -- "break (sans étiquette)" --> A
    P -- "break allees" --> F["après la boucle extérieure"]
    P -- "continue allees" --> A
```

### Côte à côte

<table>
<tr><th>Avant : drapeau</th><th>C# 15 : étiquette</th></tr>
<tr><td valign="top">

```csharp
bool trouve = false;
for (…)
{
  for (…)
  {
    if (vide) { …; trouve = true; break; }
  }
  if (trouve) break;
}
```

</td><td valign="top">

```csharp
allees: for (…)
{
  for (…)
  {
    if (vide) { …; break allees; }
  }
}

```

</td></tr>
</table>

Le code produit est le même qu'avec `goto` : un saut direct. Pas de variable supplémentaire, pas de test répété à
chaque tour de la boucle extérieure.

---

## 4. Résultats de l'exemple

```
── 05 · break et continue étiquetés (C# 15) ─────────────────
   ▸ Avant : drapeau booléen, ou goto
     avec drapeau : (0, 2)
     avec goto    : (0, 2)
   ▸ Après : allees: for (…) { for (…) { … break allees; } }
     première rupture : allée 0, emplacement 2
     ✓ même résultat que les deux versions d'avant
   ▸ continue allees; : sauter directement à l'allée suivante
     allées sans aucune rupture : 1
     ✓ seule l'allée 1 est complète
```

---

## 5. Pièges et limites

1. **L'étiquette se place juste devant la boucle** (`for`, `foreach`, `while`, `do`) ou devant un `switch`.
   `break nom;` ne peut viser qu'une instruction qui **englobe** le `break`.
2. **`break` et `continue` sans étiquette ne changent pas** : ils visent toujours la boucle la plus proche.
3. **Deux boucles imbriquées, c'est bien ; quatre, c'est une méthode qui se cache.** L'étiquette évite le drapeau,
   pas le besoin de découper un code trop profond.

## Sources

- [What's new in C# 15 — Labeled break and continue](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
