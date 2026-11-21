using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using static DotNet11Features.Display;

namespace DotNet11Features.Examples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 12 · Numbers: Decimal32/64/128, TryParsePartial, Random.NextInteger<T>, hexadecimal floats,
//      Complex<T>, correctly rounded conversions (.NET 11)
// Documentation: docs/en/12-numbers.md · docs/fr/12-nombres.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E12_Numbers
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ── Before: extracting the number at the start of an input ("42.5 kg") meant delimiting it yourself ──
    static bool ReadNumberBefore(string input, out double value, out int consumed)
    {
        consumed = 0;
        while (consumed < input.Length && (char.IsDigit(input[consumed]) || input[consumed] is '.' or '-' or '+')) consumed++;
        // …and this naive split misses "1e3", "1_000", leading spaces, signs in the middle…
        return double.TryParse(input.AsSpan(0, consumed), NumberStyles.Float, Inv, out value);
    }

    public static void Run()
    {
        Title("12 · Numbers (.NET 11)");

        // ── 1. IEEE 754 decimals ────────────────────────────────────────────────────────────────────
        Section("Decimal32 / Decimal64 / Decimal128: standard IEEE 754-2019 decimals");
        double d = 0.1 + 0.2;
        Decimal64 d64 = Decimal64.Parse("0.1", Inv) + Decimal64.Parse("0.2", Inv);
        Line($"double    : 0.1 + 0.2 = {d.ToString("R", Inv)}");
        Line($"Decimal64 : 0.1 + 0.2 = {d64.ToString(Inv)}");
        Check(d != 0.3 && d64 == Decimal64.Parse("0.3", Inv), "in binary 0.1 + 0.2 ≠ 0.3; in IEEE decimal it is exact");
        Line($"size: Decimal32 = {Unsafe.SizeOf<Decimal32>()} B (7 digits), Decimal64 = {Unsafe.SizeOf<Decimal64>()} B (16 digits), Decimal128 = {Unsafe.SizeOf<Decimal128>()} B (34 digits), decimal = {sizeof(decimal)} B (28-29 digits)");
        Decimal128 huge = Decimal128.Parse("1E+6000", Inv);
        Line($"Decimal128 covers 1E-6143 to 1E+6144; decimal stops around 7.9E+28 → 1E+6000 × 10 = {(huge * Decimal128.Parse("10", Inv)).ToString(Inv)}");
        Check(Decimal64.IsNaN(Decimal64.Parse("NaN", Inv)), "like double, these types have NaN and infinities (decimal does not)");
        ulong bits = Decimal64.EncodeDecimal(d64);
        Line($"IEEE \"DPD\" encoding of 0.3 on 64 bits: 0x{bits:X16} (standard interchange format, readable by other systems)");

        // ── 2. Correctly rounded conversions ────────────────────────────────────────────────────────
        Section("double → decimal conversion: now the exact value of the double, not 15 rounded digits");
        decimal fromDouble = (decimal)1.23;
        Line($"(decimal)1.23  = {fromDouble.ToString(Inv)}");
        Line($"1.23m          = {1.23m.ToString(Inv)}");
        Check(fromDouble != 1.23m, "behavior change: the double 1.23 is really 1.229999999999999982…, and the conversion says so");
        Line("Before .NET 11, (decimal)1.23 gave 1.23: the conversion rounded to 15 significant digits.");
        Line("For an amount, write 1.23m or use decimal.Parse, never go through a double.");

        // ── 3. TryParsePartial ───────────────────────────────────────────────────────────────────────
        Section("Before: delimit the number at the start of an input yourself");
        ReadNumberBefore("42.5 kg", out double v0, out int c0);
        Line($"\"42.5 kg\" → {v0.ToString(Inv)} ({c0} characters)");

        Section("After: double.TryParsePartial reads the number and tells where it stops");
        foreach (string input in new[] { "42.5 kg", "  -1.5e3 m/s", "12,90 € TTC", "abc" })
        {
            var culture = input.Contains(',') ? CultureInfo.GetCultureInfo("fr-FR") : Inv;
            bool ok = double.TryParsePartial(input, NumberStyles.Float, culture, out double value, out int consumed);
            Line($"{"\"" + input + "\"",-16} → {(ok ? $"{value.ToString(Inv),8}, rest \"{input[consumed..].Trim()}\"" : "no number at the start")}");
        }
        double.TryParsePartial("  -1.5e3 m/s", NumberStyles.Float, Inv, out double v1, out int c1);
        Line($"\"  -1.5e3 m/s\": {c1} characters consumed = \"{"  -1.5e3 m/s"[..c1]}\"");
        Check(v1 == -1500 && "  -1.5e3 m/s"[..c1].Trim() == "-1.5e3",
            "surrounding spaces, sign and exponent handled (NumberStyles.Float also allows trailing spaces: they are consumed)");
        Check(int.TryParsePartial("123abc", NumberStyles.Integer, Inv, out int integer, out int ci) && integer == 123 && ci == 3,
            "available on every numeric type (INumberBase<T>), here int");

        // ── 4. Random.NextInteger<T> / NextBinaryFloat<T> ─────────────────────────────────────────────
        Section("Before: Random could only draw int, long and double");
        var random = new Random(2026);
        Span<byte> bytes = stackalloc byte[8];
        random.NextBytes(bytes);
        Line($"a ulong over its whole range: NextBytes + BitConverter → {BitConverter.ToUInt64(bytes)}");

        Section("After: Random.NextInteger<T>() for any integer, NextBinaryFloat<T>() for any float");
        ulong large = random.NextInteger<ulong>();
        byte die = random.NextInteger<byte>(1, 7);
        UInt128 enormous = random.NextInteger<UInt128>();
        Half half = random.NextBinaryFloat<Half>();
        Line($"NextInteger<ulong>() = {large} · NextInteger<byte>(1, 7) = {die} · NextInteger<UInt128>() = {enormous}");
        Line($"NextBinaryFloat<Half>() = {half.ToString(Inv)}");
        Check(die is >= 1 and < 7, "bounds follow the usual [min, max) convention");

        // ── 5. Hexadecimal floats ───────────────────────────────────────────────────────────────────
        Section("Hexadecimal floats: the exact binary value, with no decimal rounding");
        string hex = 0.1.ToString("X", Inv);
        Line($"0.1.ToString(\"X\") = {hex}");
        Check(double.Parse(hex, NumberStyles.HexFloat, Inv) == 0.1, "double.Parse(…, NumberStyles.HexFloat) reads back exactly the same value");

        // ── 6. Complex<T> ───────────────────────────────────────────────────────────────────────────
        Section("Complex<T>: complex numbers in float (or Half), half the size");
        var z = new Complex<float>(3f, 4f);
        Line($"Complex<float>(3, 4): {Unsafe.SizeOf<Complex<float>>()} bytes; System.Numerics.Complex (double): {Unsafe.SizeOf<Complex>()} bytes");
        Check(Unsafe.SizeOf<Complex<float>>() * 2 == Unsafe.SizeOf<Complex>(), "an array of one million complex numbers goes from 16 MB to 8 MB");
        Line($"z × z = {z * z}");
    }
}
