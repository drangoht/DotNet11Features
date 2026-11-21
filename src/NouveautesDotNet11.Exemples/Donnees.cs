using System.Text;
using System.Text.Json;

namespace NouveautesDotNet11;

/// <summary>Jeu de données reproductible (graine fixe) partagé par les exemples et les benchmarks.</summary>
public static class Donnees
{
    public record Article(string Sku, string Libelle, decimal PrixUnitaire);
    public record Ligne(string Sku, int Quantite, decimal PrixUnitaire);
    public record Commande(string Numero, int ClientId, string Ville, DateOnly Date, string Statut, List<Ligne> Lignes);

    static readonly string[] Villes = ["Lyon", "Paris", "Marseille", "Nantes", "Lille", "Bordeaux", "Toulouse", "Rennes"];
    static readonly string[] Statuts = ["brouillon", "payee", "expediee", "livree"];

    public static readonly Article[] Catalogue =
    [
        new("CAFE-250G", "Café moulu 250 g", 6.90m), new("CAFE-1KG", "Café en grains 1 kg", 22.50m),
        new("THE-VERT", "Thé vert", 8.40m), new("THE-NOIR", "Thé noir", 7.90m), new("TASSE", "Tasse en grès", 12.00m),
        new("FILTRES", "Filtres papier ×100", 3.20m), new("MOULIN", "Moulin manuel", 39.00m), new("CHOCO", "Chocolat 70 %", 4.10m),
    ];

    public static List<Commande> Commandes(int nombre, int graine = 42)
    {
        var aleatoire = new Random(graine);
        var commandes = new List<Commande>(nombre);
        for (int i = 0; i < nombre; i++)
        {
            int nbLignes = aleatoire.Next(1, 5);
            var lignes = new List<Ligne>(nbLignes);
            for (int l = 0; l < nbLignes; l++)
            {
                var a = Catalogue[aleatoire.Next(Catalogue.Length)];
                lignes.Add(new Ligne(a.Sku, aleatoire.Next(1, 6), a.PrixUnitaire));
            }
            commandes.Add(new Commande($"CMD-2026-{i + 1:D6}", aleatoire.Next(1, 2000), Villes[aleatoire.Next(Villes.Length)],
                new DateOnly(2026, 1, 1).AddDays(aleatoire.Next(320)), Statuts[aleatoire.Next(Statuts.Length)], lignes));
        }
        return commandes;
    }

    /// <summary>Un gros document JSON (toutes les commandes dans un tableau).</summary>
    public static byte[] JsonCommandes(int nombre) => JsonSerializer.SerializeToUtf8Bytes(Commandes(nombre));

    /// <summary>Beaucoup de petits messages JSON indépendants (une commande chacun), comme sur une file de messages.</summary>
    public static byte[][] MessagesJson(int nombre, int graine = 42) =>
        Commandes(nombre, graine).Select(c => JsonSerializer.SerializeToUtf8Bytes(c)).ToArray();

    public static string Texte(byte[] utf8) => Encoding.UTF8.GetString(utf8);
}
