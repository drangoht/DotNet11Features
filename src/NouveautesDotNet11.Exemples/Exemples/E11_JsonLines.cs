using System.Text;
using System.Text.Json;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 11 · JSON Lines : écrire un flux asynchrone, un objet par ligne (.NET 11)
// Documentation : docs/11-json-lines.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E11_JsonLines
{
    public record Evenement(int Sequence, string Type, string Commande);

    // Une source qui produit des événements au fil de l'eau (base de données, file de messages…).
    static async IAsyncEnumerable<Evenement> Evenements(int nombre)
    {
        string[] types = ["creee", "payee", "expediee"];
        for (int i = 1; i <= nombre; i++)
        {
            if (i % 100 == 0) await Task.Yield();   // simule une attente d'E/S de temps en temps
            yield return new Evenement(i, types[i % 3], $"CMD-{i / 3 + 1:D4}");
        }
    }

    // ── Avant : une boucle qui sérialise chaque objet puis écrit le saut de ligne à la main ─────────
    static async Task EcrireJsonLinesAvant(Stream sortie, IAsyncEnumerable<Evenement> source)
    {
        byte[] sautDeLigne = "\n"u8.ToArray();
        await foreach (var evenement in source)
        {
            await JsonSerializer.SerializeAsync(sortie, evenement);   // attention : jamais WriteIndented ici
            await sortie.WriteAsync(sautDeLigne);
        }
    }

    public static async Task Executer()
    {
        Titre("11 · JSON Lines avec JsonSerializer.SerializeAsyncEnumerable (.NET 11)");

        Section("Avant : boucle await foreach + SerializeAsync + \\n à la main");
        using var avant = new MemoryStream();
        await EcrireJsonLinesAvant(avant, Evenements(3));
        Console.Write(Indenter(Encoding.UTF8.GetString(avant.ToArray())));

        Section("Après : SerializeAsyncEnumerable(flux, source, topLevelValues: true)");
        using var apres = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(apres, Evenements(3), topLevelValues: true);
        string jsonl = Encoding.UTF8.GetString(apres.ToArray());
        Console.Write(Indenter(jsonl));
        Verifier(apres.ToArray().AsSpan().SequenceEqual(avant.ToArray()), "octet pour octet le même résultat que la boucle manuelle");
        Verifier(jsonl.EndsWith('\n') && !jsonl.Contains('\r'), "chaque ligne, y compris la dernière, se termine par \\n (jamais \\r\\n, même sous Windows)");

        Section("Sans topLevelValues : un seul tableau JSON, écrit lui aussi au fil de l'eau");
        using var tableau = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(tableau, Evenements(3));
        Ligne(Encoding.UTF8.GetString(tableau.ToArray()));

        Section("Les options d'indentation sont ignorées : une valeur = une ligne, toujours");
        using var indente = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(indente, Evenements(2), topLevelValues: true, new JsonSerializerOptions { WriteIndented = true });
        Verifier(Encoding.UTF8.GetString(indente.ToArray()).Count(c => c == '\n') == 2, "WriteIndented = true ne casse pas le format (2 objets = 2 lignes)");

        Section("Aller-retour : DeserializeAsyncEnumerable(…, topLevelValues: true) relit le fichier");
        using var gros = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(gros, Evenements(10_000), topLevelValues: true);
        gros.Position = 0;
        int lus = 0, derniere = 0;
        await foreach (var e in JsonSerializer.DeserializeAsyncEnumerable<Evenement>(gros, topLevelValues: true))
        {
            lus++;
            derniere = e!.Sequence;
        }
        Ligne($"{gros.Length / 1024} Ko écrits, {lus} événements relus, dernier n° {derniere}");
        Verifier(lus == 10_000 && derniere == 10_000, "les 10 000 événements sont relus dans l'ordre");
    }

    static string Indenter(string texte) => string.Concat(texte.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => $"     {l}\n"));
}
