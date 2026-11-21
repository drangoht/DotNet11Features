# 14 · `EqualityComparer<T>.Create(clé)` et `BitArray` (.NET 11)

🇬🇧 [English version](../en/14-comparers-bitarray.md)

> **En une phrase** : `EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase)` remplace une
> classe `IEqualityComparer<T>` complète, et `BitArray` se construit depuis un span et compte ses bits avec `PopCount()`.
>
> Code : [`E14_Comparers.cs`](../../src/DotNet11Features.Examples/Examples/E14_Comparers.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- comparers`

---

## 1. `EqualityComparer<T>.Create` avec un sélecteur de clé

### L'idée expliquée simplement

Deux fiches contact `anne@example.com` et `ANNE@example.com` désignent la même personne. Pour dédoublonner, il faut
expliquer à .NET **ce que « égal » veut dire** pour un contact : « même e-mail, sans tenir compte de la casse ».

Un comparateur d'égalité doit fournir **deux** choses cohérentes : `Equals` (sont-ils égaux ?) et `GetHashCode` (dans
quel tiroir ranger l'objet pour le retrouver vite). Les oublier ou les rendre incohérents est une source de bogues
classique : un `HashSet` qui accepte des doublons.

### Avant

```csharp
public sealed class ContactByEmailBefore : IEqualityComparer<Contact>
{
    public bool Equals(Contact? x, Contact? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && StringComparer.OrdinalIgnoreCase.Equals(x.Email, y.Email));

    public int GetHashCode(Contact obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Email);
}
```

.NET 8 avait ajouté `EqualityComparer<T>.Create(equals, getHashCode)`, mais il fallait encore écrire les deux
fonctions, et garder leur cohérence soi-même.

### .NET 11

```csharp
var byEmail = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);

Contacts.Distinct(byEmail);
HashSet<Contact> set = [with(byEmail), .. Contacts];      // avec les arguments de collection (exemple 03)
new Dictionary<Contact, int>(byEmail);
```

On donne **la clé** et, en option, le comparateur de cette clé ; `Equals` et `GetHashCode` en sont déduits, toujours
cohérents.

### Benchmark : `Distinct` sur 10 000 contacts (4 000 uniques)

| Méthode | Temps | Mémoire |
|---|---:|---:|
| Classe `IEqualityComparer<Contact>` écrite à la main | 266 µs | 197,6 Ko |
| `EqualityComparer<Contact>.Create(c => c?.Email, …)` | 270 µs | 197,6 Ko |

Pas de différence significative : l'appel du délégué coûte quelques nanosecondes, noyées dans le calcul du hachage
de la chaîne. On gagne une classe, pas des performances.

---

## 2. `BitArray` : construction depuis un span, et `PopCount`

Un appareil renvoie un masque de 32 options sur 4 octets ; on veut savoir combien sont actives.

<table>
<tr><th>Avant</th><th>.NET 11</th></tr>
<tr><td valign="top">

```csharp
// BitArray n'acceptait qu'un byte[] :
// copie si les octets sont dans un span
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

`PopCount` utilise l'instruction processeur `POPCNT` (compte des bits à 1 sur 64 bits d'un coup), au lieu d'une boucle
bit par bit avec un booléen boxé par itération dans le `foreach` non générique. Constructeurs ajoutés :
`ReadOnlySpan<byte>`, `ReadOnlySpan<bool>`, `ReadOnlySpan<int>`.

---

## 3. Sortie de l'exemple

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

## 4. Pièges et limites

1. **Le sélecteur reçoit `T?`** : écrivez `c => c?.Email` et non `c => c.Email`, sinon le compilateur signale CS8602
   (déréférencement possible de `null`). Rencontré en écrivant l'exemple.
2. **La clé doit être stable** : si l'e-mail d'un contact change pendant qu'il est dans un `HashSet`, il devient
   introuvable (c'était déjà vrai avec une classe maison).

## Sources

- [What's new in .NET 11 libraries — Collections](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
