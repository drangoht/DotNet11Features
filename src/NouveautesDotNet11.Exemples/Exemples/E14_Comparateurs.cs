using System.Collections;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 14 · EqualityComparer<T>.Create(clé) et BitArray (span, PopCount) (.NET 11)
// Documentation : docs/14-comparateurs-bitarray.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public record Contact(string Email, string Nom, string Source);

// ── Avant : une classe complète pour dire « deux contacts sont égaux si leur e-mail l'est » ──────
public sealed class ContactParEmailAvant : IEqualityComparer<Contact>
{
    public bool Equals(Contact? x, Contact? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && StringComparer.OrdinalIgnoreCase.Equals(x.Email, y.Email));

    public int GetHashCode(Contact obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Email);
}

public static class E14_Comparateurs
{
    static readonly Contact[] Contacts =
    [
        new("anne@exemple.fr", "Anne", "boutique"),
        new("ANNE@exemple.fr", "Anne D.", "newsletter"),
        new("paul@exemple.fr", "Paul", "boutique"),
        new("Paul@Exemple.fr", "P. Martin", "salon"),
        new("lea@exemple.fr", "Léa", "salon"),
    ];

    public static void Executer()
    {
        Titre("14 · EqualityComparer<T>.Create et BitArray (.NET 11)");

        Section("Avant : une classe IEqualityComparer<Contact> de 8 lignes");
        var uniquesAvant = Contacts.Distinct(new ContactParEmailAvant()).ToList();
        Ligne(string.Join(", ", uniquesAvant.Select(c => c.Nom)));

        Section("Après : EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase)");
        var parEmail = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);
        var uniques = Contacts.Distinct(parEmail).ToList();
        Ligne(string.Join(", ", uniques.Select(c => c.Nom)));
        Verifier(uniques.SequenceEqual(uniquesAvant), "même résultat, sans classe dédiée");

        HashSet<Contact> ensemble = [with(parEmail), .. Contacts];   // avec les arguments de collection de l'exemple 03
        Verifier(ensemble.Count == 3 && ensemble.Contains(new Contact("LEA@EXEMPLE.FR", "?", "?")),
            "réutilisable partout : HashSet, Dictionary, Distinct, GroupBy, Union…");

        Section("BitArray : construit depuis un span, et PopCount pour compter les bits à 1");
        // Un masque d'options produit renvoyé par un appareil : 4 octets = 32 options.
        ReadOnlySpan<byte> masque = [0b1011_0001, 0b0000_1111, 0b1000_0000, 0b0000_0011];

        // Avant : il fallait un byte[] (donc souvent une copie), puis une boucle pour compter.
        var bitsAvant = new BitArray(masque.ToArray());
        int actifsAvant = 0;
        foreach (bool b in bitsAvant) if (b) actifsAvant++;
        Ligne($"avant : new BitArray(masque.ToArray()) + boucle foreach → {actifsAvant} options actives");

        // Après : directement depuis le span, et un comptage matériel (instruction POPCNT).
        var bits = new BitArray(masque);
        Ligne($"après : new BitArray(masque).PopCount() → {bits.PopCount()} options actives");
        Verifier(bits.PopCount() == actifsAvant && actifsAvant == 11, "11 bits à 1 sur 32 (4 + 4 + 1 + 2)");
    }
}
