# 01 · Unions (C# 15)

🇬🇧 [English version](../en/01-unions.md)

> **En une phrase** : `union PaymentMethod(Card, BankTransfer, Voucher);` déclare une valeur qui est **exactement
> l'un** de ces trois types, et le compilateur vérifie que chaque `switch` les traite tous.
>
> Code : [`E01_Unions.cs`](../../src/DotNet11Features.Examples/Examples/E01_Unions.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- unions`

---

## 1. L'idée expliquée simplement

### L'image du guichet de paiement

À la caisse, un client paie **soit** par carte, **soit** par virement, **soit** avec un bon d'achat. Jamais deux à la
fois, jamais « autre chose ». Le caissier a une fiche réflexe avec trois cases, et il sait qu'elle est complète.

En C#, ces trois moyens de paiement sont souvent **trois types sans rapport entre eux** : une `Card` vient du module
bancaire, un `Voucher` du module marketing. Ils n'ont pas de classe de base commune, et on ne veut pas leur en
inventer une juste pour pouvoir les ranger dans la même variable.

Une **union** dit au compilateur : « cette variable contient l'un de ces types, et rien d'autre ». Il peut alors
vérifier la fiche réflexe à votre place : si un `switch` oublie le bon d'achat, ça ne compile pas.

### Ce que les autres langages appellent ainsi

| Langage | Nom | Exemple |
|---|---|---|
| F# | *discriminated union* | `type Payment = Card of string \| Transfer of string` |
| TypeScript | *union type* | `type Payment = Card \| BankTransfer \| Voucher` |
| Rust | `enum` avec données | `enum Payment { Card(String), Transfer(String) }` |
| **C# 15** | **`union`** | `union PaymentMethod(Card, BankTransfer, Voucher);` |

La variante C# ressemble le plus à TypeScript : elle **regroupe des types existants**, au lieu de déclarer de
nouveaux cas.

---

## 2. Comment on faisait avant

Trois solutions courantes, toutes imparfaites :

| Solution | Problème |
|---|---|
| `object` + `switch` | Aucune garantie : on peut y ranger n'importe quoi. Un `_ => throw` obligatoire. |
| Classe de base abstraite commune | Impossible si on ne possède pas les types (`int`, `Guid`, un type d'une autre bibliothèque). |
| Bibliothèque `OneOf<T1, T2, T3>` | Paquet tiers, méthode `.Match(…)` à lambdas, types génériques illisibles dans les signatures. |

La version « maison » typique, écrite à la main :

```csharp
public sealed class PaymentMethodBefore
{
    private readonly object _value;
    private PaymentMethodBefore(object value) => _value = value;

    public static implicit operator PaymentMethodBefore(Card c) => new(c);
    public static implicit operator PaymentMethodBefore(BankTransfer t) => new(t);
    public static implicit operator PaymentMethodBefore(Voucher v) => new(v);

    public T Match<T>(Func<Card, T> card, Func<BankTransfer, T> transfer, Func<Voucher, T> voucher) => _value switch
    {
        Card c => card(c),
        BankTransfer t => transfer(t),
        Voucher v => voucher(v),
        _ => throw new InvalidOperationException("Impossible case… until someone adds a type."),
    };
}
```

18 lignes de plomberie, et le dernier bras `_ => throw` montre bien le problème : **le compilateur ne sait pas** que
la liste est complète.

---

## 3. Ce que change C# 15

```csharp
public union PaymentMethod(Card, BankTransfer, Voucher);

static string Describe(PaymentMethod method) => method switch
{
    Card c         => $"card ending in {c.Last4Digits}",
    BankTransfer t => $"transfer from {t.Iban[..4]}…",
    Voucher v      => $"voucher {v.Code} ({v.Balance} €)",
};                                      // ← pas de « _ => » : le compilateur sait que c'est complet
```

- **Conversion implicite** depuis chaque type : `PaymentMethod m = new Card("4242");`
- **`switch` exhaustif** : si on ajoute `Cheque` à l'union, chaque `switch` qui l'oublie déclenche **CS8509**.
- **Types qu'on ne possède pas** : `union CustomerId(int, Guid, string);` fonctionne.

### Ce que le compilateur génère vraiment

L'exemple l'inspecte par réflexion :

```
PaymentMethod is a struct implementing IUnion
Constructors: Card, BankTransfer, Voucher
Attributes: UnionAttribute
✓ a union is a struct (the wrapper itself is not allocated)
✓ the active case is stored in a Value property of type object
```

Autrement dit, `union PaymentMethod(Card, BankTransfer, Voucher)` devient à peu près :

```csharp
[Union]
public struct PaymentMethod : IUnion
{
    public PaymentMethod(Card value)         => Value = value;
    public PaymentMethod(BankTransfer value) => Value = value;
    public PaymentMethod(Voucher value)      => Value = value;
    public object? Value { get; }        // le cas actif
}
```

```mermaid
flowchart LR
    subgraph Pile["Pile (struct PaymentMethod)"]
        V["Value : object"]
    end
    subgraph Tas
        C["Card { 4242 }"]
        I["int 42 (boxé, 24 octets)"]
    end
    V -- "cas référence : simple pointeur" --> C
    V -. "cas valeur : une boîte est allouée" .-> I
```

### Côte à côte

<table>
<tr><th>Avant : enveloppe maison</th><th>C# 15 : union</th></tr>
<tr><td valign="top">

```csharp
public sealed class PaymentMethodBefore
{
  private readonly object _value;
  // + constructeur privé
  // + 3 opérateurs implicites
  // + méthode Match(…) à 3 lambdas
  // + bras _ => throw
}

string s = method.Match(
  card: c => …,
  transfer: t => …,
  voucher: v => …);
```

</td><td valign="top">

```csharp
public union PaymentMethod(
  Card, BankTransfer, Voucher);




string s = method switch
{
  Card c => …,
  BankTransfer t => …,
  Voucher v => …,
};
```

</td></tr>
<tr><td>

**~18 lignes · une classe (allocation à chaque valeur)**<br>
Oubli d'un cas : découvert **à l'exécution**.

</td><td>

**1 ligne · un struct**<br>
Oubli d'un cas : **CS8509 à la compilation**.

</td></tr>
</table>

---

## 4. Résultats

### Sortie de l'exemple

Le code et la sortie console sont en anglais ; la sortie est reproduite telle quelle.

```
── 01 · Unions (C# 15) ──────────────────────────────────────
   ▸ After: public union PaymentMethod(Card, BankTransfer, Voucher);
     card ending in 4242
     transfer from FR76…
     voucher XMAS26 (25 €)

   ▸ A union of types you do not own: int, Guid, string
     1234                                   → C-001234
     3f2504e0-4f89-11d3-9a0c-0305e82c3301   → G-3f2504e0
     vip-smith                              → VIP-SMITH

   ▸ The hidden cost: a value type is boxed into Value
     CustomerId id = 42;    → 24 bytes allocated (the int is boxed)
     CustomerId id = "vip"; → 0 byte allocated (a reference is just copied)
     ✓ boxed int = 24 bytes, string = 0 byte
```

### Benchmarks (BenchmarkDotNet, 1 000 valeurs)

| Scénario | Temps | Mémoire |
|---|---:|---:|
| Avant : `object[]` + `switch` avec bras `_ => throw` | 1,74 µs | 0 |
| Après : `PaymentMethod[]` (union de classes) + `switch` exhaustif | 1,85 µs (+6 %) | **0** |
| Un `int` traité directement | 0,21 µs | 0 |
| Le même `int` rangé dans `CustomerId` puis `switch` | 1,27 µs (×6,1) | 24 000 o |

- **Une union de classes n'alloue rien** et coûte à peu près autant qu'un `object` (+6 % en job BenchmarkDotNet
  complet : lire `Value` à travers le struct ajoute un chargement ; les jobs courts ont donné entre −1 % et +39 %,
  donc « à peu près pareil »). Ce qu'on gagne, c'est la vérification à la compilation.
- **Une union qui contient un type valeur alloue une boîte à chaque affectation** (24 octets pour un `int`).

Classes `UnionBenchmarks` et `UnionBoxingBenchmarks`, voir aussi le [README](../../README.fr.md#4-résultats-mesurés).

---

## 5. Pièges et limites

1. **Un `int`, un `Guid`, un `DateTime` dans une union sont boxés** (24 octets pour un `int`, 32 pour un `Guid`).
   Pour des unions de classes ou de records, aucune allocation : l'enveloppe est un struct qui ne contient qu'une
   référence. Sur un chemin très chaud avec des types valeur, mesurez.
2. **`default(PaymentMethod)` n'a aucun cas actif** : `Value` vaut `null`, et un `switch` lève
   `SwitchExpressionException`. Le compilateur prévient (CS8655, « le modèle 'null' n'est pas couvert ») quand il ne
   peut pas prouver que la valeur a été initialisée. Un champ non initialisé ou un tableau `new PaymentMethod[10]`
   contiennent ce cas vide.
3. **CS8509 n'est qu'un avertissement par défaut.** Pour que l'oubli d'un cas bloque vraiment la compilation, ce
   dépôt le passe en erreur dans `Directory.Build.props` :
   ```xml
   <WarningsAsErrors>$(WarningsAsErrors);CS8509</WarningsAsErrors>
   ```
4. **Union ou hiérarchie fermée ?** Si vous possédez les types et qu'ils partagent un sens commun (les états d'une
   commande), préférez une [hiérarchie fermée](02-hierarchies-fermees.md). L'union sert à regrouper des types
   **existants et indépendants**.
5. **Sérialisation JSON** : prise en charge par System.Text.Json en .NET 11, avec un piège quand deux cas ont la même
   forme JSON (`Guid` et `string` sont tous deux des chaînes). Voir [l'exemple 10](10-json-unions.md).

## Sources

- [What's new in C# 15 — Union types](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
- [C# 15 union types (blog .NET)](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/)
