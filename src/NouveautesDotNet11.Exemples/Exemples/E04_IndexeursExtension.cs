using System.Text.Json;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 04 · Indexeurs d'extension (C# 15) : ajouter des [ ] à un type qu'on ne possède pas
// Documentation : docs/04-indexeurs-extension.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public record LigneCommande(string Sku, string Libelle, int Quantite);

// ── Avant : une méthode d'extension. Lisible, mais on perd la syntaxe [ ] et l'affectation. ────
public static class ExtensionsAvant
{
    public static JsonElement Prop(this JsonElement element, string nom) => element.GetProperty(nom);
    public static LigneCommande ParSku(this IReadOnlyList<LigneCommande> lignes, string sku) => lignes.First(l => l.Sku == sku);
}

// ── Après (.NET 11) : un bloc extension (C# 14) peut maintenant déclarer un indexeur ────────────
public static class ExtensionsIndexeurs
{
    // JsonElement n'a pas d'indexeur par nom de propriété (JsonNode, si) : on lui en donne un.
    extension(JsonElement element)
    {
        public JsonElement this[string nom] => element.GetProperty(nom);
    }

    // Une liste de lignes de commande, indexée par code article.
    extension(IReadOnlyList<LigneCommande> lignes)
    {
        public LigneCommande this[string sku] => lignes.First(l => l.Sku == sku);
    }

    // Générique : un indexeur « depuis la fin » sur n'importe quelle séquence.
    extension<T>(IEnumerable<T> source)
    {
        public T this[Index position] => source.ElementAt(position);
    }
}

public static class E04_IndexeursExtension
{
    const string Json = """
        { "commande": { "numero": "CMD-2026-0042", "client": { "nom": "Dupont", "ville": "Lyon" } } }
        """;

    public static void Executer()
    {
        Titre("04 · Indexeurs d'extension (C# 15)");
        using JsonDocument doc = JsonDocument.Parse(Json);
        JsonElement racine = doc.RootElement;

        Section("Avant : GetProperty en cascade, ou une méthode d'extension");
        Ligne(racine.GetProperty("commande").GetProperty("client").GetProperty("ville").GetString()!);
        Ligne(racine.Prop("commande").Prop("client").Prop("ville").GetString()!);

        Section("Après : racine[\"commande\"][\"client\"][\"ville\"]");
        string ville = racine["commande"]["client"]["ville"].GetString()!;
        Ligne(ville);
        Verifier(ville == "Lyon", "l'indexeur d'extension se chaîne comme un indexeur natif");

        Section("Un indexeur métier sur IReadOnlyList<LigneCommande>");
        IReadOnlyList<LigneCommande> lignes = [new("CAFE-250G", "Café moulu", 2), new("THE-VERT", "Thé vert", 1)];
        Ligne($"lignes[\"THE-VERT\"] → {lignes["THE-VERT"].Libelle} × {lignes["THE-VERT"].Quantite}");
        Verifier(lignes["CAFE-250G"].Quantite == 2, "recherche par code article avec la syntaxe [ ]");
        Verifier(lignes[0].Sku == "CAFE-250G", "l'indexeur natif lignes[0] reste prioritaire et inchangé");

        Section("Un indexeur générique sur IEnumerable<T>, avec ^1 (depuis la fin)");
        IEnumerable<int> nombres = Enumerable.Range(1, 10).Where(n => n % 3 == 0);
        Ligne($"nombres = {string.Join(", ", nombres)} ; nombres[^1] = {nombres[^1]}");
        Verifier(nombres[^1] == 9, "^1 sur une séquence LINQ sans .ToList()");
    }
}
