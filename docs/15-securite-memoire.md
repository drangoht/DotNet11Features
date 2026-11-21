# 15 · Nouveau modèle de sécurité mémoire (C# 15, **preview**)

> **En une phrase** : déclarer un pointeur, prendre une adresse ou utiliser `fixed` ne demandent plus `unsafe` ;
> seul le **déréférencement** est dangereux et doit être marqué, au plus près, avec `unsafe { }` ou `unsafe(expr)`.
> Et `unsafe` sur une méthode signifie désormais « **l'appelant** doit l'assumer ».
>
> Code : [`src/NouveautesDotNet11.SecuriteMemoire/Program.cs`](../src/NouveautesDotNet11.SecuriteMemoire/Program.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.SecuriteMemoire`

> ⚠️ **Fonctionnalité en preview dans .NET 11.** Elle demande trois réglages, d'où un projet séparé : le reste de la
> solution reste en C# 15 stable. Syntaxe et règles peuvent encore changer en .NET 12.

```xml
<PropertyGroup>
  <LangVersion>preview</LangVersion>
  <Features>$(Features);updated-memory-safety-rules</Features>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

---

## 1. L'idée expliquée simplement

### Qu'est-ce qui est vraiment dangereux avec un pointeur ?

Un pointeur est une **adresse** : « la case mémoire n° 0x7FF6… ». Avoir une adresse dans une variable ne fait de mal à
personne. Ce qui est dangereux, c'est d'**aller lire ou écrire à cette adresse** sans vérification : si elle est
fausse (hors du tableau, objet libéré), on lit n'importe quoi ou on écrase la mémoire d'autre chose.

| Opération | Exemple | Dangereuse ? |
|---|---|---|
| Déclarer un pointeur | `int* p;` | non |
| Prendre une adresse | `&valeur` | non |
| Épingler un tableau | `fixed (int* p = tableau)` | non |
| Taille d'un type | `sizeof(MonStruct)` | non |
| **Lire / écrire à l'adresse** | `*p`, `p[i]`, `p->x` | **oui** |
| **Appeler un pointeur de fonction** | `fn(…)` | **oui** |

Jusqu'à C# 14, **toutes** ces opérations exigeaient un contexte `unsafe`. Résultat : on marque une méthode entière
`unsafe`, et un relecteur ne sait plus quelle ligne mérite son attention.

### Le parallèle avec Rust

C'est exactement le modèle de Rust : `unsafe { }` autour des seules opérations dangereuses, et `unsafe fn` pour une
fonction dont l'**appelant** doit garantir des conditions (« l'index est dans le tableau »).

---

## 2. Comment on faisait avant (C# 14)

```csharp
public static unsafe int SommeAvant(int[] valeurs)      // toute la méthode est unsafe
{
    int total = 0;
    fixed (int* debut = valeurs)
    {
        for (int i = 0; i < valeurs.Length; i++) total += debut[i];
    }
    return total;
}

int s = Calculs.SommeAvant(mesures);                    // l'appelant ne voit rien
```

Ce même code, compilé avec les nouvelles règles, **ne compile plus** (erreurs relevées en écrivant l'exemple) :

```
error CS9360: Cette opération ne peut être utilisée que dans un contexte non sécurisé       ← debut[i]
error CS9362: « Calculs.SommeAvant(int[]) » doit être utilisé dans un contexte non sécurisé,
              car il est marqué comme « unsafe »                                           ← chez l'appelant
```

Deux changements de sens apparaissent :

1. `unsafe` sur une méthode **ne rend plus son corps unsafe** : `debut[i]` doit être marqué lui-même.
2. `unsafe` sur une méthode **impose un contexte unsafe à l'appelant**.

---

## 3. Ce que change C# 15 (preview)

```csharp
public static int Somme(int[] valeurs)                   // méthode ordinaire
{
    int total = 0;
    fixed (int* debut = valeurs)                         // sûr : épingler et prendre l'adresse
    {
        for (int i = 0; i < valeurs.Length; i++)
        {
            unsafe { total += debut[i]; }                // ← la seule ligne à relire avec attention
        }
    }
    return total;
}

public static byte PremierOctet(int valeur)
{
    int* adresse = &valeur;                              // sûr
    byte* octet = (byte*)adresse;                        // sûr
    return unsafe(*octet);                               // nouvelle expression unsafe(…)
}

/// <remarks>Sécurité : l'appelant garantit que index est dans le tableau pointé.</remarks>
public static unsafe int LireSansControle(int* valeurs, int index) => unsafe(valeurs[index]);

// Côté appelant :
// Calculs.LireSansControle(p, 1);                       ← CS9362
int lu = unsafe(Calculs.LireSansControle(p, 1));         // l'appelant « signe » qu'il respecte le contrat
```

```mermaid
flowchart LR
    subgraph Avant["C# 14"]
        M1["unsafe int Somme(…)<br/>toute la méthode"] --> X1["appelant : rien à signaler"]
    end
    subgraph Apres["C# 15 preview"]
        M2["int Somme(…)<br/>unsafe { debut[i] } sur 1 ligne"] --> X2["appelant : rien à signaler"]
        M3["unsafe int LireSansControle(…)<br/>[RequiresUnsafe]"] --> X3["appelant : unsafe(…) obligatoire"]
    end
```

### Ce que le compilateur inscrit dans le binaire

```
[System.Runtime.CompilerServices.MemorySafetyRulesAttribute((Int32)2)]      ← sur le module
attributs de LireSansControle : RequiresUnsafeAttribute
```

Les autres assemblies voient donc l'exigence : une bibliothèque compilée avec les nouvelles règles impose
`unsafe(…)` aux projets qui l'utilisent, s'ils sont eux aussi compilés avec ces règles.

---

## 4. Résultats de l'exemple

```
── 15 · Nouveau modèle de sécurité mémoire (C# 15, preview) ──
   ▸ Pointeurs, & et fixed sans « unsafe » ; seul le déréférencement est marqué
     somme = 31
     ✓ le seul bloc unsafe de la méthode entoure la lecture debut[i]
   ▸ unsafe(expression) : une seule expression dangereuse au milieu de code sûr
     premier octet de 0x11223344 en mémoire : 0x44
     ✓ l'ordre des octets en mémoire est lu à travers un pointeur
   ▸ Une méthode marquée unsafe : c'est désormais l'APPELANT qui doit l'assumer
     LireSansControle(p, 1) = 20
     ✓ l'appel compile seulement à l'intérieur de unsafe(…)
   ▸ Ce que le compilateur inscrit dans le binaire
     [System.Runtime.CompilerServices.MemorySafetyRulesAttribute((Int32)2)]
     ✓ l'assembly déclare les règles avec lesquelles elle a été compilée
     attributs de LireSansControle : RequiresUnsafeAttribute
     ✓ une méthode unsafe porte [RequiresUnsafe] : les autres assemblies voient l'exigence
```

Aucun impact sur le code produit : ce sont des règles de **compilation**. Le code machine est identique.

---

## 5. Pièges et limites

1. **Preview** : `LangVersion=preview` active aussi toutes les autres fonctionnalités en preview du compilateur.
   À réserver à un projet isolé, comme ici.
2. **Migrer du code existant n'est pas transparent** : chaque méthode `unsafe` change de sens (cf. § 2). Il faut
   ajouter des `unsafe { }` dans les corps et chez les appelants.
3. Le nouveau mot-clé contextuel `safe` permet de déclarer sûrs des membres `extern` ou des champs de structures à
   disposition explicite ; non montré ici.

## Sources

- [What's new in C# 15 — Memory safety (preview)](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
