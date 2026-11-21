using System.Diagnostics;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 02 · Hiérarchies fermées (C# 15) : « ces classes dérivées, et aucune autre »
// Documentation : docs/02-hierarchies-fermees.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// ── Avant : une classe abstraite. N'importe qui, n'importe où, peut en dériver. ─────────────────
public abstract record EtatCommandeAvant;
public record BrouillonAvant : EtatCommandeAvant;
public record PayeeAvant(DateOnly Le) : EtatCommandeAvant;
public record ExpedieeAvant(string NumeroSuivi) : EtatCommandeAvant;

// ── Après (.NET 11) : le mot-clé closed. Seul cet assembly peut déclarer des dérivées directes. ──
public closed record EtatCommande;
public record Brouillon : EtatCommande;
public record Payee(DateOnly Le) : EtatCommande;
public record Expediee(string NumeroSuivi) : EtatCommande;

public static class E02_HierarchiesFermees
{
    // Avant : le compilateur ne peut pas savoir qu'il n'existe que trois états.
    // Sans le dernier bras, il signale CS8509 ; avec, une erreur de logique devient une exception à l'exécution.
    static string LibelleAvant(EtatCommandeAvant etat) => etat switch
    {
        BrouillonAvant => "brouillon",
        PayeeAvant p => $"payée le {p.Le:dd/MM}",
        ExpedieeAvant e => $"expédiée ({e.NumeroSuivi})",
        _ => throw new UnreachableException($"État inconnu : {etat.GetType().Name}"),
    };

    // Après : trois bras, c'est complet. Ajouter un état « Livree » dans ce fichier fait échouer
    // la compilation ici (CS8509 en erreur), au lieu de lever UnreachableException en production.
    static string Libelle(EtatCommande etat) => etat switch
    {
        Brouillon => "brouillon",
        Payee p => $"payée le {p.Le:dd/MM}",
        Expediee e => $"expédiée ({e.NumeroSuivi})",
    };

    // La déconstruction positionnelle des records fonctionne comme d'habitude.
    static bool PeutEtreAnnulee(EtatCommande etat) => etat switch
    {
        Brouillon or Payee => true,
        Expediee(_) => false,
    };

    public static void Executer()
    {
        Titre("02 · Hiérarchies fermées (C# 15)");

        Section("Avant : abstract record + bras « _ => throw »");
        EtatCommandeAvant[] anciens = [new BrouillonAvant(), new PayeeAvant(new DateOnly(2026, 11, 10)), new ExpedieeAvant("COLIS-42")];
        foreach (var etat in anciens) Ligne(LibelleAvant(etat));

        Section("Après : public closed record EtatCommande;");
        EtatCommande[] etats = [new Brouillon(), new Payee(new DateOnly(2026, 11, 10)), new Expediee("COLIS-42")];
        foreach (var etat in etats) Ligne($"{Libelle(etat),-24} annulable : {(PeutEtreAnnulee(etat) ? "oui" : "non")}");

        Section("Ce que le compilateur génère vraiment");
        Type t = typeof(EtatCommande);
        var attribut = t.GetCustomAttributesData().Single(a => a.AttributeType.Name == "IsClosedTypeAttribute");
        var derivees = (IEnumerable<System.Reflection.CustomAttributeTypedArgument>)attribut.NamedArguments.Single().TypedValue.Value!;
        Ligne($"[IsClosedType(DerivedTypes = {string.Join(", ", derivees.Select(d => ((Type)d.Value!).Name))})]");
        Verifier(t.IsAbstract, "une classe closed est implicitement abstraite (new EtatCommande() est interdit)");
        Verifier(derivees.Count() == 3, "la liste des dérivées directes est inscrite dans l'assembly");
    }
}
