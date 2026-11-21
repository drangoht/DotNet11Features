using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 03 · Arguments dans les expressions de collection : [with(...), ...] (C# 15)
// Documentation : docs/03-arguments-collection.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E03_ArgumentsCollection
{
    static readonly string[] Etiquettes = ["Promo", "PROMO", "promo", "Nouveau", "nouveau", "Soldes"];

    // ── Avant : dès qu'il faut un comparateur ou une capacité, on quitte la syntaxe [ … ] ─────────
    static HashSet<string> EtiquettesUniquesAvant()
    {
        var resultat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in Etiquettes) resultat.Add(e);
        return resultat;
    }

    // ── Après : with(...) en premier élément transmet les arguments au constructeur ─────────────
    static HashSet<string> EtiquettesUniques() => [with(StringComparer.OrdinalIgnoreCase), .. Etiquettes];

    static List<int> ListeAvecCapacite(int[] source) => [with(capacity: source.Length * 2), .. source];

    public static void Executer()
    {
        Titre("03 · Arguments dans les expressions de collection (C# 15)");

        Section("Avant : new HashSet<string>(StringComparer.OrdinalIgnoreCase) + boucle");
        Ligne(string.Join(", ", EtiquettesUniquesAvant()));

        Section("Après : [with(StringComparer.OrdinalIgnoreCase), .. Etiquettes]");
        var uniques = EtiquettesUniques();
        Ligne(string.Join(", ", uniques));
        Verifier(uniques.Count == 3, "6 étiquettes → 3 uniques sans tenir compte de la casse");

        Section("Dictionnaire insensible à la casse");
        Dictionary<string, decimal> prix = [with(StringComparer.OrdinalIgnoreCase)];
        prix["CAFE-250G"] = 6.90m;
        Verifier(prix["cafe-250g"] == 6.90m, "prix[\"cafe-250g\"] retrouve la clé \"CAFE-250G\"");

        Section("Capacité réservée d'avance : moins de réallocations");
        int[] source = Enumerable.Range(1, 1000).ToArray();
        var liste = ListeAvecCapacite(source);
        Verifier(liste.Capacity == 2000, $"[with(capacity: 2000), .. source] → Capacity = {liste.Capacity}");

        // Mesure : remplir une liste élément par élément avec et sans capacité réservée.
        long Mesurer(Func<List<int>> creer)
        {
            long avant = GC.GetAllocatedBytesForCurrentThread();
            for (int r = 0; r < 100; r++)
            {
                var l = creer();
                for (int i = 0; i < 10_000; i++) l.Add(i);
            }
            return (GC.GetAllocatedBytesForCurrentThread() - avant) / 100;
        }
        long sans = Mesurer(() => []);
        long avec = Mesurer(() => [with(capacity: 10_000)]);
        Ligne($"10 000 Add sans capacité : {sans,7:N0} octets alloués (13 tableaux successifs : 4, 8, 16… 16 384 cases)");
        Ligne($"10 000 Add avec capacité : {avec,7:N0} octets alloués (un seul tableau)");
        Verifier(avec < sans / 2, "réserver la capacité divise les allocations par plus de 2");
    }
}
