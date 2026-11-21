using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 13 · Texte : string et Rune, Utf16.IsValid, Base64 en chaînes, Regex AnyNewLine, StringBuilder.MoveChunks
// Documentation : docs/13-texte.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E13_Texte
{
    public static void Executer()
    {
        Titre("13 · Texte et Unicode (.NET 11)");

        // ── 1. string accepte des Rune ──────────────────────────────────────────────────────────────
        // Un emoji comme 🍵 occupe DEUX char (une « paire de substitution ») : ce n'est pas un char.
        // Rune représente un caractère Unicode complet, mais string ne savait pas le chercher.
        string avis = "Service 🍵 parfait 🍵 je recommande";
        Rune the = Rune.GetRuneAt("🍵", 0);

        Section("Avant : convertir la Rune en string pour chaque recherche");
        Ligne($"avis.Contains(the.ToString()) = {avis.Contains(the.ToString())}");
        Ligne($"avis.Replace(the.ToString(), \"☕\") = {avis.Replace(the.ToString(), "☕")}");

        Section("Après : Contains, IndexOf, Replace, Split, Trim… acceptent directement une Rune");
        Ligne($"avis.Contains(the) = {avis.Contains(the)} · avis.IndexOf(the) = {avis.IndexOf(the)}");
        Ligne($"avis.Replace(the, new Rune('☕')) = {avis.Replace(the, new Rune('☕'))}");
        string[] morceaux = avis.Split(the, StringSplitOptions.TrimEntries);
        Ligne($"avis.Split(the, TrimEntries) = [{string.Join(" | ", morceaux)}]");
        Verifier(avis.Contains(the) && morceaux.Length == 3, "recherche et découpage sur un caractère hors du plan de base, sans allocation intermédiaire");
        Verifier('É'.Equals('é', StringComparison.CurrentCultureIgnoreCase), "char.Equals('É', 'é', StringComparison…) : comparer deux char avec une règle de comparaison");

        // ── 2. Valider de l'UTF-16 ──────────────────────────────────────────────────────────────────
        Section("Utf16.IsValid : repérer une chaîne coupée au milieu d'un emoji");
        string tronque = avis[..9];   // coupe la paire de substitution de 🍵 en deux
        Ligne($"\"{avis[..11]}\"[..9] → IsValid = {Utf16.IsValid(tronque)}, premier caractère invalide à l'index {Utf16.IndexOfInvalidSubsequence(tronque)}");
        Verifier(Utf16.IsValid(avis) && !Utf16.IsValid(tronque), "avant, il fallait parcourir les char et tester char.IsSurrogatePair soi-même");

        // ── 3. Base64 vers et depuis des chaînes ──────────────────────────────────────────────────────
        Section("Base64 : la classe performante (System.Buffers.Text) sait maintenant produire des chaînes");
        byte[] cle = [0xDE, 0xAD, 0xBE, 0xEF, 0x20, 0x26];
        string avant = Convert.ToBase64String(cle);
        string apres = Base64.EncodeToString(cle);
        Ligne($"Convert.ToBase64String = {avant} · Base64.EncodeToString = {apres}");
        Span<char> tampon = stackalloc char[16];
        Base64.TryEncodeToChars(cle, tampon, out int ecrits);
        Ligne($"Base64.TryEncodeToChars dans un tampon sur la pile : {tampon[..ecrits]} (zéro allocation)");
        Verifier(avant == apres && Base64.DecodeFromChars(apres).AsSpan().SequenceEqual(cle), "une seule API pour UTF-8, char et string, dans les deux sens");

        // ── 4. Regex : tous les sauts de ligne ────────────────────────────────────────────────────────
        Section("Regex : RegexOptions.AnyNewLine reconnaît \\r\\n, \\r, \\n, \\u2028…");
        string journal = "ligne1\r\nligne2\rligne3" + (char)0x2028 + "ligne4";
        int sans = Regex.Matches(journal, @"^ligne\d$", RegexOptions.Multiline).Count;
        int avec = Regex.Matches(journal, @"^ligne\d$", RegexOptions.Multiline | RegexOptions.AnyNewLine).Count;
        Ligne($"^ligne\\d$ en Multiline : {sans} correspondance(s) ; avec AnyNewLine : {avec}");
        Verifier(sans < 4 && avec == 4, "avant, ^ et $ ne connaissaient que \\n : un fichier Windows (\\r\\n) faisait échouer $");

        // ── 5. StringBuilder.MoveChunks ───────────────────────────────────────────────────────────────
        Section("StringBuilder.MoveChunks : transférer le contenu sans le copier");
        var tampons = new StringBuilder();
        for (int i = 0; i < 10_000; i++) tampons.Append("ligne de journal ").Append(i).Append('\n');
        long octetsAvant = GC.GetAllocatedBytesForCurrentThread();
        var copie = new StringBuilder(tampons.ToString());   // avant : ToString() puis nouvelle copie
        long coutCopie = GC.GetAllocatedBytesForCurrentThread() - octetsAvant;
        octetsAvant = GC.GetAllocatedBytesForCurrentThread();
        var deplace = StringBuilder.MoveChunks(tampons);
        long coutDeplacement = GC.GetAllocatedBytesForCurrentThread() - octetsAvant;
        Ligne($"copie via ToString : {coutCopie / 1024:N0} Ko alloués · MoveChunks : {coutDeplacement} octets");
        Verifier(tampons.Length == 0 && deplace.Length == copie.Length, "la source est vidée, la destination récupère ses morceaux tels quels");
        Verifier(coutDeplacement < 1024, "MoveChunks n'alloue presque rien, quelle que soit la taille");
    }
}
