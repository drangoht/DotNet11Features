# 12 · Numbers: `Decimal64`, `TryParsePartial`, `Random.NextInteger<T>`… (.NET 11)

🇫🇷 [Version française](../fr/12-nombres.md)

> **In one sentence**: standard IEEE 754 decimals (`Decimal32/64/128`), partial number parsing (`TryParsePartial`),
> random draws of any numeric type, hexadecimal floats, `Complex<T>`… and **a behavior change** to know about on
> `(decimal)someDouble`.
>
> Code: [`E12_Numbers.cs`](../../src/DotNet11Features.Examples/Examples/E12_Numbers.cs) ·
> Run: `dotnet run -c Release --project src/DotNet11Features.Examples -- numbers`

---

## 1. IEEE 754 decimals: `Decimal32`, `Decimal64`, `Decimal128`

### The idea, simply

A `double` stores numbers **in binary**. And 0.1 cannot be written exactly in binary, just as 1/3 cannot be written
exactly in decimal (0.333…). Hence the famous:

```
double    : 0.1 + 0.2 = 0.30000000000000004
Decimal64 : 0.1 + 0.2 = 0.3
```

.NET already had `decimal` (`System.Decimal`), exact in base 10. Why three more types?

| | `decimal` | `Decimal32` | `Decimal64` | `Decimal128` |
|---|---|---|---|---|
| Size | 16 bytes | 4 bytes | 8 bytes | 16 bytes |
| Significant digits | 28-29 | 7 | 16 | 34 |
| Range | ±7.9 × 10²⁸ | ±10⁹⁶ | ±10³⁸⁴ | ±10⁶¹⁴⁴ |
| NaN, infinities | no | yes | yes | yes |
| Format | .NET-specific | **IEEE 754-2019**, standard | same | same |

The key point is the **standard format**: the same as the C23 `_Decimal32/64/128` types, the hardware decimal
arithmetic of IBM POWER and IBM Z processors, or DB2's `DECFLOAT` type. `Decimal64.EncodeDecimal(x)` produces the
exact 64 bits another system expects.

```
IEEE "DPD" encoding of 0.3 on 64 bits: 0x2234000000000003
```

They support **generic math** (`INumber<T>`…): an algorithm written once for `T : IFloatingPoint<T>` works on
`double`, `float`, `Half` and these three types.

**When to use them?** For interoperability with those systems, or when `Decimal64` (8 bytes) is enough and memory
matters (arrays of prices). For internal financial computations, `decimal` remains the simple choice.

---

## 2. ⚠️ Behavior change: `(decimal)someDouble`

```csharp
decimal d = (decimal)1.23;
// .NET 10 (checked with SDK 10.0.301): 1.23
// .NET 11                            : 1.2299999999999999822364316060
```

The `double` literal `1.23` is really 1.229999999999999982236431605997495353221893310546875 (the nearest binary
value). Up to .NET 10, the conversion **rounded to 15 significant digits**, which "fell back" on 1.23 by luck. .NET 11
does a **correctly rounded** conversion: it gives the true value of the `double`, cut to the 28 digits of `decimal`.

There is **no compatibility switch**. If your code converts `double` to `decimal` for amounts (a JSON read as
`double`, a `float` database column…), check it before migrating:

```csharp
decimal good1 = 1.23m;                                   // decimal literal
decimal good2 = decimal.Parse("1.23", Inv);              // from text
decimal good3 = Math.Round((decimal)doubleValue, 2);     // if all you have is a double: round explicitly
```

Same thing for `BigInteger` → `double`, now correctly rounded.

---

## 3. `TryParsePartial`: read the number at the start of an input

### Before

```csharp
// Delimit the number at the start of "42.5 kg" yourself
int end = 0;
while (end < input.Length && (char.IsDigit(input[end]) || input[end] is '.' or '-' or '+')) end++;
double.TryParse(input.AsSpan(0, end), NumberStyles.Float, Inv, out double value);
// …and this naive split misses "1e3", thousands separators, leading spaces, signs in the middle…
```

### .NET 11

```csharp
double.TryParsePartial("  -1.5e3 m/s", NumberStyles.Float, Inv, out double value, out int consumed);
// value = -1500, consumed = 9 → the rest "m/s" starts at input[9]
```

Available on **every numeric type** through `INumberBase<T>` (`int`, `decimal`, `Half`, `Decimal64`…), for `string`,
`ReadOnlySpan<char>` and UTF-8 (`ReadOnlySpan<byte>`). Culture and `NumberStyles` rules are exactly those of
`TryParse`.

```
     "42.5 kg"        →     42.5, rest "kg"
     "  -1.5e3 m/s"   →    -1500, rest "m/s"
     "12,90 € TTC"    →     12.9, rest "€ TTC"        (fr-FR culture)
     "abc"            → no number at the start
```

---

## 4. `Random.NextInteger<T>()` and `NextBinaryFloat<T>()`

`Random` could only draw `int`, `long` and `double`. For a `ulong` over its whole range, a `UInt128` or a `Half`,
you went through `NextBytes` and rebuilt the number (with bias traps).

```csharp
ulong large      = Random.Shared.NextInteger<ulong>();
byte  die        = Random.Shared.NextInteger<byte>(1, 7);     // [1, 7)
UInt128 enormous = Random.Shared.NextInteger<UInt128>();
Half half        = Random.Shared.NextBinaryFloat<Half>();     // [0, 1)
```

---

## 5. Hexadecimal floats

```csharp
0.1.ToString("X")                                   // "0X1.999999999999AP-4"
double.Parse("0X1.999999999999AP-4", NumberStyles.HexFloat) == 0.1   // true, to the bit
```

Hexadecimal notation shows **the exact binary value** of a float, with no decimal rounding: handy to debug
computation differences, write exact constants, or exchange with C/C++ (`printf`'s `%a`).

---

## 6. `Complex<T>`

`System.Numerics.Complex` uses `double` (16 bytes). `Complex<T>` accepts `float`, `Half`, `BFloat16` and the
decimals: `Complex<float>` fits in **8 bytes**, half the memory for signal or audio processing where `float`
precision is enough.

---

## 7. Output of the example

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

## 8. Pitfalls and limits

1. **The `double` → `decimal` conversion change** (§ 2) is the only item in this repository that can **change the
   result of existing code** without compiling it differently. Test it before any migration.
2. **`TryParsePartial` also consumes trailing spaces** if `NumberStyles` allows them (`Float` does):
   `"  -1.5e3 m/s"` consumes 9 characters, space included.
3. Operations on `Decimal64`/`Decimal128` are **done in software** (no CPU instruction on x64/Arm64): slower than
   `double`, comparable to `decimal`.

## Sources

- [What's new in .NET 11 libraries — Numerics](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
- [IEEE 754-2019](https://standards.ieee.org/ieee/754/6210/)
