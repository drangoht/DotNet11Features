using System.Text.Json;
using System.Text.Json.Serialization;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 10 · System.Text.Json : unions, hiérarchies fermées, politique de nommage par type (.NET 11)
// Documentation : docs/10-json-unions.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// ── Une réponse de prestataire de paiement : deux formes d'objet, sans champ « type » ──────────
public record PaiementAccepte(string Autorisation, decimal Montant);
public record PaiementRefuse(string Motif, int CodeErreur);

// Après : une union + le classificateur structurel, qui choisit le cas d'après les propriétés présentes.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union ReponsePaiement(PaiementAccepte, PaiementRefuse);

// Une union de valeurs simples : le type de jeton JSON (nombre, chaîne) suffit à choisir le cas.
public union Mesure(double, string);

// Avant : un convertisseur écrit à la main qui inspecte le JSON pour deviner le type.
public sealed class ReponsePaiementConvertisseurAvant : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader lecteur, Type type, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref lecteur);
        if (doc.RootElement.TryGetProperty("Autorisation", out _)) return doc.RootElement.Deserialize<PaiementAccepte>(options)!;
        if (doc.RootElement.TryGetProperty("Motif", out _)) return doc.RootElement.Deserialize<PaiementRefuse>(options)!;
        throw new JsonException("Réponse de paiement inconnue");
    }

    public override void Write(Utf8JsonWriter ecrivain, object valeur, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(ecrivain, valeur, valeur.GetType(), options);
}

// ── Hiérarchie fermée : le polymorphisme est déduit, plus besoin de lister chaque dérivée ──────
// Avant : un [JsonDerivedType] par classe dérivée, à tenir à jour à la main.
[JsonPolymorphic]
[JsonDerivedType(typeof(RemisePourcentageAvant), "RemisePourcentageAvant")]
[JsonDerivedType(typeof(RemiseMontantAvant), "RemiseMontantAvant")]
public abstract record RemiseAvant;
public record RemisePourcentageAvant(int Pourcentage) : RemiseAvant;
public record RemiseMontantAvant(decimal Montant) : RemiseAvant;

// Après : closed + InferClosedTypePolymorphism. Ajouter une dérivée ne demande rien de plus.
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record Remise;
public record RemisePourcentage(int Pourcentage) : Remise;
public record RemiseMontant(decimal Montant) : Remise;

// ── Politique de nommage portée par le type ─────────────────────────────────────────────────────
// Avant : un [JsonPropertyName("snake_case")] sur chaque propriété pour ce seul type.
public record WebhookAvant(
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("amount_cents")] long AmountCents);

// Après : un seul attribut sur le type, le reste de l'application garde sa politique globale.
[JsonNamingPolicy(JsonKnownNamingPolicy.SnakeCaseLower)]
public record Webhook(string EventId, DateTimeOffset CreatedAt, long AmountCents);

public static class E10_JsonUnions
{
    public static void Executer()
    {
        Titre("10 · System.Text.Json : unions et hiérarchies fermées (.NET 11)");

        Section("Union de valeurs simples : écrite telle quelle, sans enveloppe ni $type");
        Mesure[] mesures = [21.5, "capteur hors ligne"];
        string jsonMesures = JsonSerializer.Serialize(mesures);
        Ligne(jsonMesures);
        Verifier(jsonMesures == """[21.5,"capteur hors ligne"]""", "seule la valeur du cas actif est écrite");
        var relues = JsonSerializer.Deserialize<Mesure[]>(jsonMesures)!;
        Verifier(relues[0].Value is double && relues[1].Value is string, "à la lecture, un nombre donne le cas double, une chaîne le cas string");

        Section("Avant : un JsonConverter maison pour deviner la forme de la réponse");
        var optionsAvant = new JsonSerializerOptions { Converters = { new ReponsePaiementConvertisseurAvant() } };
        object ancienne = JsonSerializer.Deserialize<object>("""{"Motif":"Fonds insuffisants","CodeErreur":51}""", optionsAvant)!;
        Ligne($"{ancienne.GetType().Name} : {ancienne}");

        Section("Après : [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]");
        string[] reponses = ["""{"Autorisation":"A1B2C3","Montant":42.5}""", """{"Motif":"Fonds insuffisants","CodeErreur":51}"""];
        foreach (string json in reponses)
        {
            ReponsePaiement reponse = JsonSerializer.Deserialize<ReponsePaiement>(json);
            string texte = reponse switch
            {
                PaiementAccepte a => $"accepté, autorisation {a.Autorisation} pour {a.Montant} €",
                PaiementRefuse r => $"refusé ({r.CodeErreur}) : {r.Motif}",
            };
            Ligne($"{json,-55} → {texte}");
        }
        Verifier(JsonSerializer.Deserialize<ReponsePaiement>(reponses[1]).Value is PaiementRefuse, "le cas est choisi d'après les noms de propriétés présents");
        Verifier(JsonSerializer.Serialize<ReponsePaiement>(new PaiementAccepte("A1B2C3", 42.5m)) == reponses[0], "à l'écriture, l'objet du cas actif est écrit sans enveloppe");
        try
        {
            JsonSerializer.Deserialize<ReponsePaiement>("""{"Inconnu":true}""");
            Verifier(false, "un objet qui ne ressemble à aucun cas aurait dû être refusé");
        }
        catch (JsonException) { Verifier(true, "un objet qui ne correspond à aucun cas lève JsonException"); }

        Section("Hiérarchie fermée : InferClosedTypePolymorphism remplace la liste des [JsonDerivedType]");
        RemiseAvant[] remisesAvant = [new RemisePourcentageAvant(10), new RemiseMontantAvant(5m)];
        Remise[] remises = [new RemisePourcentage(10), new RemiseMontant(5m)];
        Ligne($"avant : {JsonSerializer.Serialize(remisesAvant)}");
        string jsonRemises = JsonSerializer.Serialize(remises);
        Ligne($"après : {jsonRemises}");
        var relu = JsonSerializer.Deserialize<Remise[]>(jsonRemises)!;
        Verifier(relu[0] is RemisePourcentage { Pourcentage: 10 } && relu[1] is RemiseMontant { Montant: 5m }, "aller-retour complet, sans un seul [JsonDerivedType]");

        Section("Politique de nommage au niveau du type : [JsonNamingPolicy(SnakeCaseLower)]");
        var date = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        string avant = JsonSerializer.Serialize(new WebhookAvant("evt_42", date, 4250));
        string apres = JsonSerializer.Serialize(new Webhook("evt_42", date, 4250));
        Ligne($"avant (3 attributs) : {avant}");
        Ligne($"après (1 attribut)  : {apres}");
        Verifier(avant == apres, "même JSON, un attribut au lieu d'un par propriété");

        Section("Et la nouvelle politique globale JsonNamingPolicy.PascalCase");
        var pascal = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.PascalCase };
        string jsonPascal = JsonSerializer.Serialize(new { numeroCommande = "C-001", montantTtc = 42.5 }, pascal);
        Ligne(jsonPascal);
        Verifier(jsonPascal == """{"NumeroCommande":"C-001","MontantTtc":42.5}""", "des propriétés camelCase écrites en PascalCase");
    }
}
