using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 12 · Nombres : Decimal32/64/128, TryParsePartial, Random.NextInteger<T>, flottants en hexadécimal,
//      Complex<T>, conversions correctement arrondies (.NET 11)
// Documentation : docs/12-nombres.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E12_Nombres
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ── Avant : extraire le nombre au début d'une saisie (« 42.5 kg ») demandait de le délimiter soi-même ──
    static bool LireNombreAvant(string saisie, out double valeur, out int consommes)
    {
        consommes = 0;
        while (consommes < saisie.Length && (char.IsDigit(saisie[consommes]) || saisie[consommes] is '.' or '-' or '+')) consommes++;
        // …et ce découpage naïf rate « 1e3 », « 1_000 », les espaces de tête, les signes au milieu…
        return double.TryParse(saisie.AsSpan(0, consommes), NumberStyles.Float, Inv, out valeur);
    }

    public static void Executer()
    {
        Titre("12 · Nombres (.NET 11)");

        // ── 1. Décimaux IEEE 754 ─────────────────────────────────────────────────────────────────────
        Section("Decimal32 / Decimal64 / Decimal128 : des décimaux standard IEEE 754-2019");
        double d = 0.1 + 0.2;
        Decimal64 d64 = Decimal64.Parse("0.1", Inv) + Decimal64.Parse("0.2", Inv);
        Ligne($"double    : 0.1 + 0.2 = {d.ToString("R", Inv)}");
        Ligne($"Decimal64 : 0.1 + 0.2 = {d64.ToString(Inv)}");
        Verifier(d != 0.3 && d64 == Decimal64.Parse("0.3", Inv), "en binaire 0.1 + 0.2 ≠ 0.3 ; en décimal IEEE, c'est exact");
        Ligne($"taille : Decimal32 = {Unsafe.SizeOf<Decimal32>()} o (7 chiffres), Decimal64 = {Unsafe.SizeOf<Decimal64>()} o (16 chiffres), Decimal128 = {Unsafe.SizeOf<Decimal128>()} o (34 chiffres), decimal = {sizeof(decimal)} o (28-29 chiffres)");
        Decimal128 tres = Decimal128.Parse("1E+6000", Inv);
        Ligne($"Decimal128 couvre 1E-6143 à 1E+6144 ; decimal s'arrête vers 7,9E+28 → 1E+6000 × 10 = {(tres * Decimal128.Parse("10", Inv)).ToString(Inv)}");
        Verifier(Decimal64.IsNaN(Decimal64.Parse("NaN", Inv)), "comme double, ces types connaissent NaN et l'infini (decimal non)");
        ulong bits = Decimal64.EncodeDecimal(d64);
        Ligne($"encodage IEEE « DPD » de 0.3 sur 64 bits : 0x{bits:X16} (format d'échange normalisé, lisible par d'autres systèmes)");

        // ── 2. Conversions correctement arrondies ─────────────────────────────────────────────────────
        Section("Conversion double → decimal : désormais la valeur exacte du double, pas 15 chiffres arrondis");
        decimal depuisDouble = (decimal)1.23;
        Ligne($"(decimal)1.23  = {depuisDouble.ToString(Inv)}");
        Ligne($"1.23m          = {1.23m.ToString(Inv)}");
        Verifier(depuisDouble != 1.23m, "changement de comportement : le double 1.23 vaut en réalité 1.229999999999999982…, et la conversion le dit");
        Ligne("Avant .NET 11, (decimal)1.23 donnait 1.23 : la conversion arrondissait à 15 chiffres significatifs.");
        Ligne("Pour un montant, écrivez 1.23m ou passez par decimal.Parse, jamais par un double.");

        // ── 3. TryParsePartial ───────────────────────────────────────────────────────────────────────
        Section("Avant : délimiter soi-même le nombre au début d'une saisie");
        LireNombreAvant("42.5 kg", out double v0, out int c0);
        Ligne($"\"42.5 kg\" → {v0.ToString(Inv)} ({c0} caractères)");

        Section("Après : double.TryParsePartial lit le nombre et dit où il s'arrête");
        foreach (string saisie in new[] { "42.5 kg", "  -1.5e3 m/s", "12,90 € TTC", "abc" })
        {
            var culture = saisie.Contains(',') ? CultureInfo.GetCultureInfo("fr-FR") : Inv;
            bool ok = double.TryParsePartial(saisie, NumberStyles.Float, culture, out double valeur, out int consommes);
            Ligne($"{"\"" + saisie + "\"",-16} → {(ok ? $"{valeur.ToString(Inv),8}, reste « {saisie[consommes..].Trim()} »" : "pas de nombre au début")}");
        }
        double.TryParsePartial("  -1.5e3 m/s", NumberStyles.Float, Inv, out double v1, out int c1);
        Ligne($"\"  -1.5e3 m/s\" : {c1} caractères consommés = « {"  -1.5e3 m/s"[..c1]} »");
        Verifier(v1 == -1500 && "  -1.5e3 m/s"[..c1].Trim() == "-1.5e3",
            "espaces autour, signe et exposant gérés (NumberStyles.Float autorise aussi les espaces de fin : ils sont consommés)");
        Verifier(int.TryParsePartial("123abc", NumberStyles.Integer, Inv, out int entier, out int ce) && entier == 123 && ce == 3,
            "disponible sur tous les types numériques (INumberBase<T>), ici int");

        // ── 4. Random.NextInteger<T> / NextBinaryFloat<T> ─────────────────────────────────────────────
        Section("Avant : Random ne savait tirer que des int, des long et des double");
        var hasard = new Random(2026);
        Span<byte> octets = stackalloc byte[8];
        hasard.NextBytes(octets);
        Ligne($"un ulong sur toute la plage : NextBytes + BitConverter → {BitConverter.ToUInt64(octets)}");

        Section("Après : Random.NextInteger<T>() pour n'importe quel entier, NextBinaryFloat<T>() pour n'importe quel flottant");
        ulong grand = hasard.NextInteger<ulong>();
        byte de = hasard.NextInteger<byte>(1, 7);
        UInt128 enorme = hasard.NextInteger<UInt128>();
        Half demi = hasard.NextBinaryFloat<Half>();
        Ligne($"NextInteger<ulong>() = {grand} · NextInteger<byte>(1, 7) = {de} · NextInteger<UInt128>() = {enorme}");
        Ligne($"NextBinaryFloat<Half>() = {demi}");
        Verifier(de is >= 1 and < 7, "les bornes suivent la convention habituelle [min, max[");

        // ── 5. Flottants en hexadécimal ─────────────────────────────────────────────────────────────
        Section("Flottants en hexadécimal : la valeur binaire exacte, sans arrondi décimal");
        string hex = 0.1.ToString("X", Inv);
        Ligne($"0.1.ToString(\"X\") = {hex}");
        Verifier(double.Parse(hex, NumberStyles.HexFloat, Inv) == 0.1, "double.Parse(…, NumberStyles.HexFloat) relit exactement la même valeur");

        // ── 6. Complex<T> ───────────────────────────────────────────────────────────────────────────
        Section("Complex<T> : des nombres complexes en float (ou Half), deux fois plus compacts");
        var z = new Complex<float>(3f, 4f);
        Ligne($"Complex<float>(3, 4) : {Unsafe.SizeOf<Complex<float>>()} octets ; System.Numerics.Complex (double) : {Unsafe.SizeOf<Complex>()} octets");
        Verifier(Unsafe.SizeOf<Complex<float>>() * 2 == Unsafe.SizeOf<Complex>(), "un tableau d'un million de complexes passe de 16 Mo à 8 Mo");
        Ligne($"z × z = {z * z}");
    }
}
