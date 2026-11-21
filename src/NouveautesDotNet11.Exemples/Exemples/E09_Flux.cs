using System.Buffers;
using System.Text;
using System.Text.Json;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 09 · Nouveaux flux : StringStream, ReadOnlyMemoryStream, WritableMemoryStream, ReadOnlySequenceStream
// Documentation : docs/09-flux.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E09_Flux
{
    static long Allocations(Action action)
    {
        action();                                              // préchauffage
        long avant = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - avant;
    }

    static string Ko(long octets) => $"{octets / 1024.0,8:N0} Ko";

    public static void Executer()
    {
        Titre("09 · Nouveaux types Stream (.NET 11)");

        // ── 1. Une chaîne vers une API qui attend un Stream ─────────────────────────────────────────
        string json = Donnees.Texte(Donnees.JsonCommandes(5_000));
        Ligne($"Une chaîne JSON de {json.Length / 1024:N0} K caractères à passer à une API qui attend un Stream");

        Section("Avant : new MemoryStream(Encoding.UTF8.GetBytes(texte)) → copie complète en octets");
        long octetsAvant = Allocations(() =>
        {
            using var flux = new MemoryStream(Encoding.UTF8.GetBytes(json));
            CompterOctets(flux);
        });
        Ligne($"mémoire allouée : {Ko(octetsAvant)}");

        Section("Après : new StringStream(texte, Encoding.UTF8) → encodage au fil de la lecture");
        long octetsApres = Allocations(() =>
        {
            using var flux = new StringStream(json, Encoding.UTF8);
            CompterOctets(flux);
        });
        Ligne($"mémoire allouée : {Ko(octetsApres)}");
        using (var verif = new StringStream(json, Encoding.UTF8))
        {
            var relues = JsonSerializer.Deserialize<List<Donnees.Commande>>(verif)!;
            Verifier(relues.Count == 5_000, "JsonSerializer.Deserialize lit directement le StringStream");
        }
        Verifier(octetsApres * 10 < octetsAvant, "StringStream alloue au moins 10 fois moins que GetBytes + MemoryStream");

        // ── 2. Une zone mémoire (pas un tableau) vue comme un Stream ─────────────────────────────────
        Section("Avant : MemoryStream exige un byte[] → ToArray() sur un ReadOnlyMemory<byte> = copie");
        byte[] fichier = Encoding.UTF8.GetBytes(json);
        ReadOnlyMemory<byte> tranche = fichier.AsMemory(1_000, 100_000);   // un morceau d'un tampon plus grand
        long copie = Allocations(() => { using var f = new MemoryStream(tranche.ToArray()); CompterOctets(f); });
        Ligne($"mémoire allouée : {Ko(copie)}");

        Section("Après : new ReadOnlyMemoryStream(tranche) → aucune copie, en lecture seule");
        long sansCopie = Allocations(() => { using var f = new ReadOnlyMemoryStream(tranche); CompterOctets(f); });
        Ligne($"mémoire allouée : {Ko(sansCopie)}");
        using (var f = new ReadOnlyMemoryStream(tranche))
        {
            Verifier(!f.CanWrite && f.Length == 100_000, "le flux est en lecture seule et fait exactement la taille de la tranche");
        }
        Verifier(sansCopie < 1024, "aucune copie de la tranche (moins de 1 Ko alloué)");

        // ── 3. Écrire dans un tampon fourni, de taille fixe ───────────────────────────────────────────
        Section("WritableMemoryStream : écrire dans un tampon loué, sans qu'il grossisse");
        byte[] loue = ArrayPool<byte>.Shared.Rent(256);
        try
        {
            using var sortie = new WritableMemoryStream(loue.AsMemory(0, 256));
            JsonSerializer.Serialize(sortie, new { Numero = "CMD-2026-000042", Montant = 42.5m });
            Ligne($"{sortie.Position} octets écrits dans le tampon loué : {Encoding.UTF8.GetString(loue, 0, (int)sortie.Position)}");
            Verifier(sortie.Position > 0, "le JSON est écrit directement dans le tampon fourni");
            try
            {
                sortie.Write(new byte[1024]);
                Verifier(false, "écrire au-delà du tampon aurait dû échouer");
            }
            catch (NotSupportedException)
            {
                Verifier(true, "dépasser la capacité lève NotSupportedException (un MemoryStream aurait réalloué)");
            }
        }
        finally { ArrayPool<byte>.Shared.Return(loue); }

        // ── 4. Une séquence en morceaux (System.IO.Pipelines) ─────────────────────────────────────────
        Section("ReadOnlySequenceStream : lire une ReadOnlySequence<byte> (Pipelines) sans l'aplatir");
        ReadOnlySequence<byte> sequence = Morceaux(fichier, tailleMorceau: 4096);
        Ligne($"séquence de {sequence.Length / 1024:N0} Ko en {Math.Ceiling(sequence.Length / 4096.0)} segments");
        long aplatie = Allocations(() => { using var f = new MemoryStream(sequence.ToArray()); CompterOctets(f); });
        long directe = Allocations(() => { using var f = new ReadOnlySequenceStream(sequence); CompterOctets(f); });
        Ligne($"avant, sequence.ToArray() + MemoryStream : {Ko(aplatie)}");
        Ligne($"après, new ReadOnlySequenceStream(seq)   : {Ko(directe)}");
        Verifier(directe < 1024, "la séquence est lue segment par segment, sans copie");
    }

    static long CompterOctets(Stream flux)
    {
        Span<byte> tampon = stackalloc byte[4096];
        long total = 0;
        int lus;
        while ((lus = flux.Read(tampon)) > 0) total += lus;
        return total;
    }

    // Découpe un tableau en segments chaînés, comme les tampons reçus d'un PipeReader.
    static ReadOnlySequence<byte> Morceaux(byte[] donnees, int tailleMorceau)
    {
        Segment? premier = null, dernier = null;
        for (int i = 0; i < donnees.Length; i += tailleMorceau)
        {
            var morceau = donnees.AsMemory(i, Math.Min(tailleMorceau, donnees.Length - i));
            if (premier is null) premier = dernier = new Segment(morceau, 0);
            else dernier = dernier!.Ajouter(morceau);
        }
        return new ReadOnlySequence<byte>(premier!, 0, dernier!, dernier!.Memory.Length);
    }

    sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memoire, long debut) { Memory = memoire; RunningIndex = debut; }
        public Segment Ajouter(ReadOnlyMemory<byte> memoire)
        {
            var suivant = new Segment(memoire, RunningIndex + Memory.Length);
            Next = suivant;
            return suivant;
        }
    }
}
