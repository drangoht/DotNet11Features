// Inclus par rapport.cs avec #:include modeles.cs

record Commande(string Numero, string Ville, decimal Montant);

static class Echantillon
{
    public const string Json = """
        [
          { "Numero": "C-001", "Ville": "Lyon", "Montant": 42.50 },
          { "Numero": "C-002", "Ville": "Paris", "Montant": 19.90 },
          { "Numero": "C-003", "Ville": "Lyon", "Montant": 7.00 },
          { "Numero": "C-004", "Ville": "Nantes", "Montant": 99.00 },
          { "Numero": "C-005", "Ville": "Paris", "Montant": 64.30 }
        ]
        """;
}
