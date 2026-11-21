// Included by report.cs with #:include models.cs

record Order(string Number, string City, decimal Amount);

static class Sample
{
    public const string Json = """
        [
          { "Number": "O-001", "City": "Lyon", "Amount": 42.50 },
          { "Number": "O-002", "City": "Paris", "Amount": 19.90 },
          { "Number": "O-003", "City": "Lyon", "Amount": 7.00 },
          { "Number": "O-004", "City": "Nantes", "Amount": 99.00 },
          { "Number": "O-005", "City": "Paris", "Amount": 64.30 }
        ]
        """;
}
