# 12 · Nombres : `Decimal64`, `TryParsePartial`, `Random.NextInteger<T>`… (.NET 11)

> **En une phrase** : des décimaux standard IEEE 754 (`Decimal32/64/128`), une lecture partielle des nombres
> (`TryParsePartial`), le tirage aléatoire de n'importe quel type numérique, les flottants en hexadécimal,
> `Complex<T>`… et **un changement de comportement** à connaître sur `(decimal)unDouble`.
>
> Code : [`E12_Nombres.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E12_Nombres.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- nombres`

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
encodage IEEE « DPD » de 0.3 sur 64 bits : 0x2234000000000003
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
decimal bon1 = 1.23m;                                 // littéral decimal
decimal bon2 = decimal.Parse("1.23", Inv);            // depuis le texte
decimal bon3 = Math.Round((decimal)valeurDouble, 2);  // si on n'a qu'un double : arrondir explicitement
```

Même chose pour `BigInteger` → `double`, désormais correctement arrondi.

---

## 3. `TryParsePartial` : lire le nombre au début d'une saisie

### Avant

```csharp
// Délimiter soi-même le nombre au début de « 42.5 kg »
int fin = 0;
while (fin < saisie.Length && (char.IsDigit(saisie[fin]) || saisie[fin] is '.' or '-' or '+')) fin++;
double.TryParse(saisie.AsSpan(0, fin), NumberStyles.Float, Inv, out double valeur);
// …et ce découpage naïf rate « 1e3 », les séparateurs de milliers, les espaces de tête, les signes au milieu…
```

### .NET 11

```csharp
double.TryParsePartial("  -1.5e3 m/s", NumberStyles.Float, Inv, out double valeur, out int consommes);
// valeur = -1500, consommes = 9 → le reste « m/s » commence à saisie[9]
```

Disponible sur **tous les types numériques** via `INumberBase<T>` (`int`, `decimal`, `Half`, `Decimal64`…), en
`string`, `ReadOnlySpan<char>` et UTF-8 (`ReadOnlySpan<byte>`). Les règles de culture et de `NumberStyles` sont
exactement celles de `TryParse`.

```
     "42.5 kg"        →     42.5, reste « kg »
     "  -1.5e3 m/s"   →    -1500, reste « m/s »
     "12,90 € TTC"    →     12.9, reste « € TTC »        (culture fr-FR)
     "abc"            → pas de nombre au début
```

---

## 4. `Random.NextInteger<T>()` et `NextBinaryFloat<T>()`

Avant, `Random` ne savait tirer que des `int`, des `long` et des `double`. Pour un `ulong` sur toute sa plage, un
`UInt128` ou un `Half`, il fallait passer par `NextBytes` et reconstituer le nombre (avec des pièges de biais).

```csharp
ulong grand   = Random.Shared.NextInteger<ulong>();
byte  de      = Random.Shared.NextInteger<byte>(1, 7);       // [1, 7[
UInt128 enorme = Random.Shared.NextInteger<UInt128>();
Half demi     = Random.Shared.NextBinaryFloat<Half>();       // [0, 1[
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

## 7. Résultats de l'exemple

```
   ▸ Decimal32 / Decimal64 / Decimal128 : des décimaux standard IEEE 754-2019
     double    : 0.1 + 0.2 = 0.30000000000000004
     Decimal64 : 0.1 + 0.2 = 0.3
     ✓ en binaire 0.1 + 0.2 ≠ 0.3 ; en décimal IEEE, c'est exact
     taille : Decimal32 = 4 o (7 chiffres), Decimal64 = 8 o (16 chiffres), Decimal128 = 16 o (34 chiffres), decimal = 16 o (28-29 chiffres)
     Decimal128 couvre 1E-6143 à 1E+6144 ; decimal s'arrête vers 7,9E+28 → 1E+6000 × 10 = 1.0E+6001
   ▸ Conversion double → decimal : désormais la valeur exacte du double, pas 15 chiffres arrondis
     (decimal)1.23  = 1.2299999999999999822364316060
     1.23m          = 1.23
   ▸ Après : Random.NextInteger<T>() pour n'importe quel entier, NextBinaryFloat<T>() pour n'importe quel flottant
     NextInteger<ulong>() = 1255648345076228814 · NextInteger<byte>(1, 7) = 4 · NextInteger<UInt128>() = 1455598176…
   ▸ Complex<T> : des nombres complexes en float (ou Half), deux fois plus compacts
     Complex<float>(3, 4) : 8 octets ; System.Numerics.Complex (double) : 16 octets
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
