# 12 · Nombres : `Decimal64`, `TryParsePartial`, `Random.NextInteger<T>`… (.NET 11)

🇬🇧 [English version](../en/12-numbers.md)

> **En une phrase** : des décimaux standard IEEE 754 (`Decimal32/64/128`), une lecture partielle des nombres
> (`TryParsePartial`), le tirage aléatoire de n'importe quel type numérique, les flottants en hexadécimal,
> `Complex<T>`… et **un changement de comportement** à connaître sur `(decimal)unDouble`.
>
> Code : [`E12_Numbers.cs`](../../src/DotNet11Features.Examples/Examples/E12_Numbers.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- numbers`

---

## 1. Les décimaux IEEE 754 : `Decimal32`, `Decimal64`, `Decimal128`

### L'idée expliquée simplement

Un `double` stocke les nombres **en binaire**. Or 0,1 ne s'écrit pas exactement en binaire, comme 1/3 ne s'écrit pas
exactement en décimal (0,333…). D'où le célèbre :

```
double    : 0.1 + 0.2 = 0.30000000000000004
Decimal64 : 0.1 + 0.2 = 0.3
```

.NET avait déjà `decimal` (`System.Decimal`), exact en base 10. Pourquoi trois types de plus ?

| | `decimal` | `Decimal32` | `Decimal64` | `Decimal128` |
|---|---|---|---|---|
| Taille | 16 octets | 4 octets | 8 octets | 16 octets |
| Chiffres significatifs | 28-29 | 7 | 16 | 34 |
| Plage | ±7,9 × 10²⁸ | ±10⁹⁶ | ±10³⁸⁴ | ±10⁶¹⁴⁴ |
| NaN, infinis | non | oui | oui | oui |
| Format | propre à .NET | **IEEE 754-2019**, normalisé | idem | idem |

Le point clé est le **format normalisé** : le même que les types `_Decimal32/64/128` du C23, l'arithmétique décimale
matérielle des processeurs IBM POWER et IBM Z, ou le type `DECFLOAT` de DB2. `Decimal64.EncodeDecimal(x)` produit
les 64 bits exacts qu'attend un autre système.

```
IEEE "DPD" encoding of 0.3 on 64 bits: 0x2234000000000003
```

Ils prennent en charge les **mathématiques génériques** (`INumber<T>`…) : un algorithme écrit une fois pour
`T : IFloatingPoint<T>` marche sur `double`, `float`, `Half` et ces trois types.

**Quand les utiliser ?** Pour l'interopérabilité avec ces systèmes, ou quand `Decimal64` (8 octets) suffit et que la
mémoire compte (tableaux de prix). Pour des calculs financiers internes, `decimal` reste le choix simple.

---

## 2. ⚠️ Changement de comportement : `(decimal)unDouble`

```csharp
decimal d = (decimal)1.23;
// .NET 10 (vérifié avec le SDK 10.0.301) : 1.23
// .NET 11                                 : 1.2299999999999999822364316060
```

Le littéral `1.23` d'un `double` vaut en réalité 1,229999999999999982236431605997495353221893310546875 (la valeur
binaire la plus proche). Jusqu'à .NET 10, la conversion **arrondissait à 15 chiffres significatifs**, ce qui
« retombait » sur 1.23 par chance. .NET 11 fait une conversion **correctement arrondie** : elle donne la vraie valeur
du `double`, tronquée aux 28 chiffres de `decimal`.

Il n'y a **pas d'interrupteur de compatibilité**. Si votre code convertit des `double` en `decimal` pour des montants
(lecture d'un JSON en `double`, colonne `float` d'une base…), vérifiez-le avant de migrer :

```csharp
decimal good1 = 1.23m;                                   // littéral decimal
decimal good2 = decimal.Parse("1.23", Inv);              // depuis le texte
decimal good3 = Math.Round((decimal)doubleValue, 2);     // si on n'a qu'un double : arrondir explicitement
```

Même chose pour `BigInteger` → `double`, désormais correctement arrondi.

---

## 3. `TryParsePartial` : lire le nombre au début d'une saisie

### Avant

```csharp
// Délimiter soi-même le nombre au début de « 42.5 kg »
int end = 0;
while (end < input.Length && (char.IsDigit(input[end]) || input[end] is '.' or '-' or '+')) end++;
double.TryParse(input.AsSpan(0, end), NumberStyles.Float, Inv, out double value);
// …et ce découpage naïf rate « 1e3 », les séparateurs de milliers, les espaces de tête, les signes au milieu…
```

### .NET 11

```csharp
double.TryParsePartial("  -1.5e3 m/s", NumberStyles.Float, Inv, out double value, out int consumed);
// value = -1500, consumed = 9 → le reste « m/s » commence à input[9]
```

Disponible sur **tous les types numériques** via `INumberBase<T>` (`int`, `decimal`, `Half`, `Decimal64`…), en
`string`, `ReadOnlySpan<char>` et UTF-8 (`ReadOnlySpan<byte>`). Les règles de culture et de `NumberStyles` sont
exactement celles de `TryParse`.

```
     "42.5 kg"        →     42.5, rest "kg"
     "  -1.5e3 m/s"   →    -1500, rest "m/s"
     "12,90 € TTC"    →     12.9, rest "€ TTC"        (culture fr-FR)
     "abc"            → no number at the start
```

---

## 4. `Random.NextInteger<T>()` et `NextBinaryFloat<T>()`

Avant, `Random` ne savait tirer que des `int`, des `long` et des `double`. Pour un `ulong` sur toute sa plage, un
`UInt128` ou un `Half`, il fallait passer par `NextBytes` et reconstituer le nombre (avec des pièges de biais).

```csharp
ulong large      = Random.Shared.NextInteger<ulong>();
byte  die        = Random.Shared.NextInteger<byte>(1, 7);     // [1, 7[
UInt128 enormous = Random.Shared.NextInteger<UInt128>();
Half half        = Random.Shared.NextBinaryFloat<Half>();     // [0, 1[
```

---

## 5. Flottants en hexadécimal

```csharp
0.1.ToString("X")                                   // "0X1.999999999999AP-4"
double.Parse("0X1.999999999999AP-4", NumberStyles.HexFloat) == 0.1   // true, au bit près
```

L'écriture hexadécimale montre **la valeur binaire exacte** d'un flottant, sans arrondi décimal : utile pour
déboguer des écarts de calcul, écrire des constantes exactes ou échanger avec du C/C++ (`%a` de `printf`).

---

## 6. `Complex<T>`

`System.Numerics.Complex` est en `double` (16 octets). `Complex<T>` accepte `float`, `Half`, `BFloat16` et les
décimaux : `Complex<float>` tient en **8 octets**, deux fois moins de mémoire pour du traitement du signal ou de
l'audio où la précision du `float` suffit.

---

## 7. Sortie de l'exemple

```
   ▸ Decimal32 / Decimal64 / Decimal128: standard IEEE 754-2019 decimals
     double    : 0.1 + 0.2 = 0.30000000000000004
     Decimal64 : 0.1 + 0.2 = 0.3
     ✓ in binary 0.1 + 0.2 ≠ 0.3; in IEEE decimal it is exact
     size: Decimal32 = 4 B (7 digits), Decimal64 = 8 B (16 digits), Decimal128 = 16 B (34 digits), decimal = 16 B (28-29 digits)
     Decimal128 covers 1E-6143 to 1E+6144; decimal stops around 7.9E+28 → 1E+6000 × 10 = 1.0E+6001
   ▸ double → decimal conversion: now the exact value of the double, not 15 rounded digits
     (decimal)1.23  = 1.2299999999999999822364316060
     1.23m          = 1.23
   ▸ After: Random.NextInteger<T>() for any integer, NextBinaryFloat<T>() for any float
     NextInteger<ulong>() = 1255648345076228814 · NextInteger<byte>(1, 7) = 4 · NextInteger<UInt128>() = 1455598176…
   ▸ Complex<T>: complex numbers in float (or Half), half the size
     Complex<float>(3, 4): 8 bytes; System.Numerics.Complex (double): 16 bytes
     z × z = <-7; 24>
```

## 8. Pièges et limites

1. **Le changement de conversion `double` → `decimal`** (§ 2) est le seul point de ce dépôt qui peut **changer le
   résultat d'un code existant** sans le recompiler différemment. À tester avant toute migration.
2. **`TryParsePartial` consomme aussi les espaces de fin** si `NumberStyles` les autorise (`Float` le fait) :
   `"  -1.5e3 m/s"` consomme 9 caractères, espace compris.
3. Les opérations sur `Decimal64`/`Decimal128` sont **logicielles** (pas d'instruction processeur sur x64/Arm64) :
   plus lentes que `double`, comparables à `decimal`.

## Sources

- [What's new in .NET 11 libraries — Numerics](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
- [IEEE 754-2019](https://standards.ieee.org/ieee/754/6210/)
