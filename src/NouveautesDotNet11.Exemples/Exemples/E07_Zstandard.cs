using System.Diagnostics;
using System.IO.Compression;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 07 · Zstandard intégré à .NET 11 : ZstandardStream, ZstandardEncoder, ZstandardDictionary
// Documentation : docs/07-zstandard.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E07_Zstandard
{
    // Le même code pour les trois algorithmes : seul le constructeur du flux change.
    static byte[] Compresser(byte[] donnees, Func<Stream, Stream> creerFlux)
    {
        using var sortie = new MemoryStream();
        using (var flux = creerFlux(sortie)) flux.Write(donnees);
        return sortie.ToArray();
    }

    static byte[] Decompresser(byte[] compresse, Func<Stream, Stream> creerFlux)
    {
        using var entree = new MemoryStream(compresse);
        using var flux = creerFlux(entree);
        using var sortie = new MemoryStream();
        flux.CopyTo(sortie);
        return sortie.ToArray();
    }

    static (double CompressionMs, double DecompressionMs, int Taille, bool Identique) Mesurer(
        byte[] donnees, Func<Stream, Stream> compresseur, Func<Stream, Stream> decompresseur)
    {
        byte[] compresse = Compresser(donnees, compresseur);           // préchauffage
        byte[] retour = Decompresser(compresse, decompresseur);
        const int Tours = 5;
        var chrono = Stopwatch.StartNew();
        for (int i = 0; i < Tours; i++) compresse = Compresser(donnees, compresseur);
        double c = chrono.Elapsed.TotalMilliseconds / Tours;
        chrono.Restart();
        for (int i = 0; i < Tours; i++) retour = Decompresser(compresse, decompresseur);
        double d = chrono.Elapsed.TotalMilliseconds / Tours;
        return (c, d, compresse.Length, retour.AsSpan().SequenceEqual(donnees));
    }

    public static void Executer()
    {
        Titre("07 · Zstandard (.NET 11)");
        byte[] json = Donnees.JsonCommandes(20_000);
        Ligne($"Données : 20 000 commandes en JSON = {json.Length / 1024.0 / 1024:F1} Mo");

        Section("Avant : GZip ou Brotli (Zstandard demandait un paquet NuGet tiers et sa DLL native)");
        Section("Après : ZstandardStream s'utilise exactement comme GZipStream");
        Ligne($"{"Algorithme",-30} {"Taille",10} {"Ratio",7} {"Compression",12} {"Décompression",14}");
        Func<Stream, Stream> lireZstd = s => new ZstandardStream(s, CompressionMode.Decompress);
        Func<Stream, Stream> Zstd(int qualite) => s => new ZstandardStream(s, new ZstandardCompressionOptions { Quality = qualite }, leaveOpen: false);
        var resultats = new Dictionary<string, (double C, double D, int Taille, bool Ok)>
        {
            ["GZip (Optimal)"] = Mesurer(json, s => new GZipStream(s, CompressionLevel.Optimal), s => new GZipStream(s, CompressionMode.Decompress)),
            ["GZip (SmallestSize)"] = Mesurer(json, s => new GZipStream(s, CompressionLevel.SmallestSize), s => new GZipStream(s, CompressionMode.Decompress)),
            ["Brotli (Optimal)"] = Mesurer(json, s => new BrotliStream(s, CompressionLevel.Optimal), s => new BrotliStream(s, CompressionMode.Decompress)),
            ["Zstandard (Optimal)"] = Mesurer(json, s => new ZstandardStream(s, CompressionLevel.Optimal), lireZstd),
            ["Zstandard (qualité 6)"] = Mesurer(json, Zstd(6), lireZstd),
            ["Zstandard (qualité 9)"] = Mesurer(json, Zstd(9), lireZstd),
            ["Zstandard (qualité 12)"] = Mesurer(json, Zstd(12), lireZstd),
            ["Zstandard (qualité 19)"] = Mesurer(json, Zstd(19), lireZstd),
        };
        foreach (var (nom, r) in resultats)
            Ligne($"{nom,-30} {r.Taille / 1024,7:N0} Ko {(double)json.Length / r.Taille,6:F1}× {r.C,9:F1} ms {r.D,11:F1} ms");
        Verifier(resultats.Values.All(r => r.Ok), "chaque algorithme restitue exactement les données d'origine");
        var zOpt = resultats["Zstandard (Optimal)"]; var gzip = resultats["GZip (Optimal)"];
        var z19 = resultats["Zstandard (qualité 19)"];
        Verifier(zOpt.C * 2 < gzip.C, "Zstandard (Optimal) compresse au moins 2 fois plus vite que GZip (Optimal)… mais produit un fichier plus gros");
        Verifier(z19.Taille < gzip.Taille * 0.8, "Zstandard (qualité 19) produit un fichier au moins 20 % plus petit que GZip (Optimal), au prix d'une compression lente");
        var decompressionsZstd = resultats.Where(r => r.Key.StartsWith("Zstandard")).Select(r => r.Value.D).ToArray();
        Verifier(decompressionsZstd.Max() < decompressionsZstd.Min() * 2, "la vitesse de décompression Zstandard ne dépend presque pas de la qualité choisie");

        Section("Petits messages : le dictionnaire entraîné change tout");
        // Un message de 300 octets se compresse mal seul : il n'y a pas assez de répétitions à l'intérieur.
        // Un dictionnaire entraîné sur des messages typiques fournit ces répétitions d'avance.
        byte[][] entrainement = Donnees.MessagesJson(2_000, graine: 1);
        byte[][] messages = Donnees.MessagesJson(1_000, graine: 2);   // d'autres messages, jamais vus à l'entraînement
        byte[] echantillons = entrainement.SelectMany(m => m).ToArray();
        int[] longueurs = entrainement.Select(m => m.Length).ToArray();
        byte[] donneesDictionnaire = ZstandardDictionary.Train(echantillons, longueurs, maxDictionarySize: 4096);
        using ZstandardDictionary dictionnaire = ZstandardDictionary.Create(donneesDictionnaire, quality: 19);

        long brut = 0, gzipSeul = 0, zstdSeul = 0, zstdDico = 0;
        bool toutRelu = true;
        byte[] tampon = new byte[64 * 1024], relu = new byte[64 * 1024];
        foreach (byte[] message in messages)
        {
            brut += message.Length;
            gzipSeul += Compresser(message, s => new GZipStream(s, CompressionLevel.Optimal)).Length;
            ZstandardEncoder.TryCompress(message, tampon, out int sansDico, quality: 19, windowLog2: ZstandardCompressionOptions.DefaultWindowLog2);
            zstdSeul += sansDico;
            ZstandardEncoder.TryCompress(message, tampon, out int avecDico, dictionnaire, windowLog2: ZstandardCompressionOptions.DefaultWindowLog2);
            zstdDico += avecDico;
            toutRelu &= ZstandardDecoder.TryDecompress(tampon.AsSpan(0, avecDico), relu, out int ecrits, dictionnaire)
                        && relu.AsSpan(0, ecrits).SequenceEqual(message);
        }
        Ligne($"1 000 messages, {brut / 1000} octets en moyenne, dictionnaire de {donneesDictionnaire.Length} octets");
        Ligne($"{"GZip, message par message",-36} {gzipSeul / 1000,5} octets/message  ({(double)brut / gzipSeul:F1}×)");
        Ligne($"{"Zstandard sans dictionnaire",-36} {zstdSeul / 1000,5} octets/message  ({(double)brut / zstdSeul:F1}×)");
        Ligne($"{"Zstandard avec dictionnaire",-36} {zstdDico / 1000,5} octets/message  ({(double)brut / zstdDico:F1}×)");
        Verifier(toutRelu, "chaque message se décompresse avec le même dictionnaire");
        Verifier(zstdDico * 2 < zstdSeul, "le dictionnaire divise au moins par deux la taille des petits messages");
    }
}
