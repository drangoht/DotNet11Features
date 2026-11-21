using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 08 · LINQ : FullJoin et jointures sans sélecteur de résultat (.NET 11) ; LeftJoin/RightJoin datent de .NET 10
// Documentation : docs/08-jointures-linq.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E08_JointuresLinq
{
    public record Client(int Id, string Nom);
    public record Commande(string Numero, int ClientId, decimal Montant);
    public record StockEntrepot(string Sku, int Quantite);
    public record ArticleCatalogue(string Sku, string Libelle);

    static readonly Client[] Clients = [new(1, "Dupont"), new(2, "Martin"), new(3, "Durand")];
    static readonly Commande[] Commandes = [new("C-001", 1, 42.50m), new("C-002", 1, 19.90m), new("C-003", 3, 7.00m), new("C-004", 9, 99.00m)];

    static readonly ArticleCatalogue[] Catalogue = [new("CAFE", "Café moulu"), new("THE", "Thé vert"), new("TASSE", "Tasse en grès")];
    static readonly StockEntrepot[] Stock = [new("CAFE", 120), new("TASSE", 8), new("FILTRE", 300)];

    public static void Executer()
    {
        Titre("08 · LINQ : FullJoin et jointures sans sélecteur (.NET 11)");

        // ── Avant : la jointure externe gauche, l'incantation GroupJoin + SelectMany + DefaultIfEmpty ──
        Section("Avant .NET 10 : GroupJoin + SelectMany + DefaultIfEmpty");
        var avant = Clients
            .GroupJoin(Commandes, c => c.Id, cmd => cmd.ClientId, (client, cmds) => new { client, cmds })
            .SelectMany(x => x.cmds.DefaultIfEmpty(), (x, cmd) => (x.client.Nom, Numero: cmd?.Numero ?? "(aucune)"));
        foreach (var (nom, numero) in avant) Ligne($"{nom,-8} {numero}");

        // ── .NET 10 : LeftJoin ─────────────────────────────────────────────────────────────────────
        Section(".NET 10 : Clients.LeftJoin(Commandes, c => c.Id, cmd => cmd.ClientId, (client, cmd) => …)");
        var gauche = Clients.LeftJoin(Commandes, c => c.Id, cmd => cmd.ClientId,
            (client, cmd) => (client.Nom, Numero: cmd?.Numero ?? "(aucune)")).ToList();
        foreach (var (nom, numero) in gauche) Ligne($"{nom,-8} {numero}");
        Verifier(gauche.SequenceEqual(avant), "exactement le même résultat que l'ancienne incantation");
        Verifier(gauche.Contains(("Martin", "(aucune)")), "Martin, sans commande, apparaît quand même (c'est le principe d'un LEFT JOIN)");

        // .NET 11 : sans sélecteur de résultat, on récupère directement des tuples (Outer, Inner).
        Section(".NET 11 : sans sélecteur, des tuples à déconstruire");
        foreach (var (client, cmd) in Clients.LeftJoin(Commandes, c => c.Id, cmd => cmd.ClientId))
            Ligne($"{client.Nom,-8} {cmd?.Montant.ToString("0.00 €") ?? "—"}");

        Section(".NET 11 : RightJoin sans sélecteur, les commandes même si le client est introuvable");
        foreach (var (client, cmd) in Clients.RightJoin(Commandes, c => c.Id, cmd => cmd.ClientId))
            Ligne($"{cmd.Numero}  client : {client?.Nom ?? $"#{cmd.ClientId} introuvable"}");
        Verifier(Clients.RightJoin(Commandes, c => c.Id, cmd => cmd.ClientId).Count(x => x.Item1 is null) == 1,
            "la commande C-004 (client 9 inexistant) est conservée, avec un client null");

        // ── FullJoin : rapprocher deux sources (catalogue et entrepôt) ─────────────────────────────
        Section("Avant : la jointure complète = jointure gauche + les orphelins de droite, à la main");
        var avantComplet = Catalogue
            .GroupJoin(Stock, a => a.Sku, s => s.Sku, (a, s) => new { a, s })
            .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => (Sku: x.a.Sku, Article: (ArticleCatalogue?)x.a, Stock: s))
            .Concat(Stock.Where(s => !Catalogue.Any(a => a.Sku == s.Sku)).Select(s => (s.Sku, Article: (ArticleCatalogue?)null, Stock: (StockEntrepot?)s)))
            .ToList();
        Ligne($"{avantComplet.Count} lignes, en deux passes et un Any() par article du stock");

        Section(".NET 11 : Catalogue.FullJoin(Stock, a => a.Sku, s => s.Sku)");
        var rapprochement = Catalogue.FullJoin(Stock, a => a.Sku, s => s.Sku).ToList();
        foreach (var (article, stock) in rapprochement)
        {
            string diagnostic = (article, stock) switch
            {
                (not null, not null) => $"OK, {stock.Quantite} en stock",
                (not null, null) => "au catalogue mais jamais reçu en entrepôt",
                (null, not null) => "en entrepôt mais absent du catalogue !",
                (null, null) => "impossible",
            };
            Ligne($"{article?.Sku ?? stock!.Sku,-7} {diagnostic}");
        }
        Verifier(rapprochement.Count == 4 && rapprochement.Count == avantComplet.Count, "4 lignes : CAFE, THE, TASSE et FILTRE, comme la version manuelle");

        Section(".NET 11 : Join sans sélecteur, avec un comparateur de clés (SKU insensibles à la casse)");
        StockEntrepot[] stockMinuscules = [new("cafe", 120)];
        int trouves = Catalogue.Join(stockMinuscules, a => a.Sku, s => s.Sku, StringComparer.OrdinalIgnoreCase).Count();
        Verifier(trouves == 1, "Join(…, StringComparer.OrdinalIgnoreCase) associe CAFE et cafe");
    }
}
