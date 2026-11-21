using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 05 · break et continue étiquetés (C# 15) : sortir d'une boucle extérieure sans drapeau ni goto
// Documentation : docs/05-break-continue-etiquetes.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E05_BoucleEtiquetee
{
    // Entrepôt : 4 allées × 5 emplacements, chaque case contient un stock.
    static readonly int[][] Entrepot =
    [
        [12, 8, 0, 5, 9],
        [7, 7, 7, 7, 7],
        [3, 0, 4, 0, 6],
        [10, 11, 12, 0, 14],
    ];

    // ── Avant (1) : un booléen « trouve » testé à chaque niveau ─────────────────────────────────
    static (int Allee, int Place)? PremiereRuptureAvecDrapeau()
    {
        (int, int)? resultat = null;
        bool trouve = false;
        for (int allee = 0; allee < Entrepot.Length; allee++)
        {
            for (int place = 0; place < Entrepot[allee].Length; place++)
            {
                if (Entrepot[allee][place] == 0) { resultat = (allee, place); trouve = true; break; }
            }
            if (trouve) break;   // à ne pas oublier, sinon on continue à chercher
        }
        return resultat;
    }

    // ── Avant (2) : goto, le seul moyen direct… et le moins aimé ────────────────────────────────
    static (int Allee, int Place)? PremiereRuptureAvecGoto()
    {
        (int, int)? resultat = null;
        for (int allee = 0; allee < Entrepot.Length; allee++)
            for (int place = 0; place < Entrepot[allee].Length; place++)
                if (Entrepot[allee][place] == 0) { resultat = (allee, place); goto Fin; }
        Fin:
        return resultat;
    }

    // ── Après (.NET 11) : on nomme la boucle et on sort d'elle directement ──────────────────────
    static (int Allee, int Place)? PremiereRupture()
    {
        (int, int)? resultat = null;
        allees: for (int allee = 0; allee < Entrepot.Length; allee++)
        {
            for (int place = 0; place < Entrepot[allee].Length; place++)
            {
                if (Entrepot[allee][place] == 0) { resultat = (allee, place); break allees; }
            }
        }
        return resultat;
    }

    // continue étiqueté : passer à l'allée suivante dès qu'une rupture est vue dans l'allée courante.
    static List<int> AlleesSansRupture()
    {
        List<int> completes = [];
        allees: foreach (var (allee, index) in Entrepot.Select((a, i) => (a, i)))
        {
            foreach (int stock in allee)
            {
                if (stock == 0) continue allees;   // inutile de regarder le reste de cette allée
            }
            completes.Add(index);
        }
        return completes;
    }

    public static void Executer()
    {
        Titre("05 · break et continue étiquetés (C# 15)");

        Section("Avant : drapeau booléen, ou goto");
        Ligne($"avec drapeau : {PremiereRuptureAvecDrapeau()}");
        Ligne($"avec goto    : {PremiereRuptureAvecGoto()}");

        Section("Après : allees: for (…) { for (…) { … break allees; } }");
        var rupture = PremiereRupture();
        Ligne($"première rupture : allée {rupture?.Allee}, emplacement {rupture?.Place}");
        Verifier(rupture == (0, 2), "même résultat que les deux versions d'avant");

        Section("continue allees; : sauter directement à l'allée suivante");
        var completes = AlleesSansRupture();
        Ligne($"allées sans aucune rupture : {string.Join(", ", completes)}");
        Verifier(completes.SequenceEqual([1]), "seule l'allée 1 est complète");
    }
}
