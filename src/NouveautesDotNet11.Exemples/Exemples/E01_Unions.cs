using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 01 · Unions (C# 15) : « une valeur qui est exactement l'un de ces types »
// Documentation : docs/01-unions.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// Des types qui existent déjà et n'ont AUCUN lien entre eux (aucune classe de base commune).
public record Carte(string Derniers4Chiffres);
public record Virement(string Iban);
public record BonAchat(string Code, decimal Solde);

// ── Après (.NET 11) : une ligne ─────────────────────────────────────────────────────────────────
public union MoyenPaiement(Carte, Virement, BonAchat);

// Une union peut même regrouper des types qu'on ne possède pas : int, Guid, string…
public union IdentifiantClient(int, Guid, string);

// ── Avant : la version « maison » la plus courante, une classe enveloppe écrite à la main ──────
public sealed class MoyenPaiementAvant
{
    private readonly object _valeur;
    private MoyenPaiementAvant(object valeur) => _valeur = valeur;

    public static implicit operator MoyenPaiementAvant(Carte c) => new(c);
    public static implicit operator MoyenPaiementAvant(Virement v) => new(v);
    public static implicit operator MoyenPaiementAvant(BonAchat b) => new(b);

    // Le compilateur ne sait pas que _valeur ne peut être que l'un des trois types :
    // chaque appelant doit écrire un cas « par défaut » qui ne devrait jamais arriver.
    public T Selon<T>(Func<Carte, T> carte, Func<Virement, T> virement, Func<BonAchat, T> bon) => _valeur switch
    {
        Carte c => carte(c),
        Virement v => virement(v),
        BonAchat b => bon(b),
        _ => throw new InvalidOperationException("Cas impossible… jusqu'au jour où quelqu'un ajoute un type."),
    };
}

public static class E01_Unions
{
    // Avant : il faut passer par la méthode Selon(…) et ses trois lambdas, ou par un switch sur un object.
    static string DecrireAvant(MoyenPaiementAvant moyen) => moyen.Selon(
        carte: c => $"carte se terminant par {c.Derniers4Chiffres}",
        virement: v => $"virement depuis {v.Iban[..4]}…",
        bon: b => $"bon d'achat {b.Code} ({b.Solde} €)");

    // Après : un switch ordinaire. Pas de « _ => » : le compilateur sait que la liste est complète.
    // Si on ajoute un 4e type à l'union, cette méthode ne compile plus (CS8509, passé en erreur dans
    // Directory.Build.props) : impossible d'oublier un endroit.
    static string Decrire(MoyenPaiement moyen) => moyen switch
    {
        Carte c => $"carte se terminant par {c.Derniers4Chiffres}",
        Virement v => $"virement depuis {v.Iban[..4]}…",
        BonAchat b => $"bon d'achat {b.Code} ({b.Solde} €)",
    };

    static string Normaliser(IdentifiantClient id) => id switch
    {
        int numero => $"C-{numero:D6}",
        Guid guid => $"G-{guid.ToString()[..8]}",
        string code => code.ToUpperInvariant(),
    };

    public static void Executer()
    {
        Titre("01 · Unions (C# 15)");

        Section("Avant : une classe enveloppe écrite à la main");
        MoyenPaiementAvant ancien = new Carte("4242");
        Ligne(DecrireAvant(ancien));

        Section("Après : public union MoyenPaiement(Carte, Virement, BonAchat);");
        MoyenPaiement[] moyens = [new Carte("4242"), new Virement("FR7630006000011234567890189"), new BonAchat("NOEL26", 25m)];
        foreach (MoyenPaiement moyen in moyens) Ligne(Decrire(moyen));

        Section("Une union de types qu'on ne possède pas : int, Guid, string");
        IdentifiantClient[] ids = [1234, Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), "vip-dupont"];
        foreach (IdentifiantClient id in ids) Ligne($"{id.Value,-38} → {Normaliser(id)}");

        Section("Ce que le compilateur génère vraiment");
        Type t = typeof(MoyenPaiement);
        Ligne($"MoyenPaiement est un {(t.IsValueType ? "struct" : "class")} qui implémente {string.Join(", ", t.GetInterfaces().Select(i => i.Name))}");
        Ligne($"Constructeurs : {string.Join(", ", t.GetConstructors().Select(c => c.GetParameters()[0].ParameterType.Name))}");
        Ligne($"Attributs : {string.Join(", ", t.GetCustomAttributesData().Select(a => a.AttributeType.Name).Where(n => !n.StartsWith("Nullable")))}");
        Verifier(t.IsValueType, "une union est un struct (pas d'allocation pour l'enveloppe elle-même)");
        Verifier(t.GetProperty("Value")?.PropertyType == typeof(object), "le cas actif est rangé dans une propriété Value de type object");

        Section("Le coût caché : un type valeur est « boxé » dans Value");
        long avant = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { IdentifiantClient id = i; _ = id.Value; }
        long octetsParInt = (GC.GetAllocatedBytesForCurrentThread() - avant) / 1000;
        avant = GC.GetAllocatedBytesForCurrentThread();
        string code = "vip";
        for (int i = 0; i < 1000; i++) { IdentifiantClient id = code; _ = id.Value; }
        long octetsParString = (GC.GetAllocatedBytesForCurrentThread() - avant) / 1000;
        Ligne($"IdentifiantClient id = 42;     → {octetsParInt} octets alloués (l'int est boxé)");
        Ligne($"IdentifiantClient id = \"vip\"; → {octetsParString} octet alloué (une référence est juste recopiée)");
        Verifier(octetsParInt == 24 && octetsParString == 0, "int boxé = 24 octets, string = 0 octet");

        Section("La valeur par défaut d'une union n'a aucun cas actif");
        MoyenPaiement vide = default;
        Verifier(vide.Value is null, "default(MoyenPaiement).Value est null");
        try { Decrire(vide); Verifier(false, "le switch aurait dû échouer"); }
        catch (System.Runtime.CompilerServices.SwitchExpressionException)
        {
            Verifier(true, "un switch sur une union vide lève SwitchExpressionException (le compilateur prévient : CS8655)");
        }
    }
}
