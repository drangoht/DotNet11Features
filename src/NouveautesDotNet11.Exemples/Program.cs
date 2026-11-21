using NouveautesDotNet11;
using NouveautesDotNet11.Exemples;

// Chaque exemple se lance seul ou tous à la suite :
//   dotnet run -c Release --project src/NouveautesDotNet11.Exemples              → tous
//   dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- unions    → un seul (nom ou numéro)
//   dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- liste     → la liste
// Chaque affirmation de la documentation est vérifiée (✓ / ✗) ; le code de sortie vaut le nombre d'échecs.

Console.OutputEncoding = System.Text.Encoding.UTF8;

(string Numero, string Cle, string Titre, Func<Task> Lancer)[] exemples =
[
    ("01", "unions", "Unions (C# 15)", Sync(E01_Unions.Executer)),
    ("02", "fermees", "Hiérarchies fermées (C# 15)", Sync(E02_HierarchiesFermees.Executer)),
    ("03", "collections", "Arguments with(...) dans [ ] (C# 15)", Sync(E03_ArgumentsCollection.Executer)),
    ("04", "indexeurs", "Indexeurs d'extension (C# 15)", Sync(E04_IndexeursExtension.Executer)),
    ("05", "boucles", "break / continue étiquetés (C# 15)", Sync(E05_BoucleEtiquetee.Executer)),
    ("06", "process", "Process.RunAndCaptureTextAsync", E06_Process.Executer),
    ("07", "zstandard", "Compression Zstandard intégrée", Sync(E07_Zstandard.Executer)),
    ("08", "jointures", "LINQ LeftJoin / RightJoin / FullJoin", Sync(E08_JointuresLinq.Executer)),
    ("09", "flux", "StringStream, ReadOnlyMemoryStream…", Sync(E09_Flux.Executer)),
    ("10", "json", "System.Text.Json : unions, hiérarchies fermées", Sync(E10_JsonUnions.Executer)),
    ("11", "jsonl", "JSON Lines : SerializeAsyncEnumerable", E11_JsonLines.Executer),
    ("12", "nombres", "Decimal64, TryParsePartial, NextInteger<T>…", Sync(E12_Nombres.Executer)),
    ("13", "texte", "Rune, Utf16, Base64, Regex AnyNewLine…", Sync(E13_Texte.Executer)),
    ("14", "comparateurs", "EqualityComparer<T>.Create, BitArray", Sync(E14_Comparateurs.Executer)),
];

string choix = args.FirstOrDefault()?.ToLowerInvariant() ?? "tout";

if (choix == "liste")
{
    foreach (var e in exemples) Console.WriteLine($"  {e.Numero}  {e.Cle,-12} {e.Titre}");
    return 0;
}

var selection = choix == "tout" ? exemples : exemples.Where(e => e.Numero == choix.PadLeft(2, '0') || e.Cle == choix).ToArray();
if (selection.Length == 0)
{
    Console.WriteLine($"Exemple inconnu : {choix}. Exemples possibles :");
    foreach (var e in exemples) Console.WriteLine($"  {e.Numero}  {e.Cle,-12} {e.Titre}");
    return -1;
}

Console.WriteLine($".NET {Environment.Version} · {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
foreach (var e in selection) await e.Lancer();

Console.WriteLine();
Console.WriteLine(Affichage.Echecs == 0
    ? $"Toutes les vérifications sont passées ({selection.Length} exemple(s))."
    : $"{Affichage.Echecs} vérification(s) en échec.");
return Affichage.Echecs;

static Func<Task> Sync(Action action) => () => { action(); return Task.CompletedTask; };
