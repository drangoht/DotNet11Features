#!/usr/bin/env dotnet
// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 16 · Applications en un fichier : #:include (SDK .NET 11)
// Documentation : docs/16-applications-fichier.md
//   dotnet run appli-fichier/rapport.cs
//   dotnet run appli-fichier/rapport.cs -- Lyon
// Pas de .csproj : les directives #: en tête de fichier remplacent le projet.
// ═════════════════════════════════════════════════════════════════════════════════════════════════
#:include modeles.cs
#:include outils/formatage.cs
#:property Company="Boutique Café & Thé"
#:property PublishAot=false

using System.Reflection;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;
string? ville = args.FirstOrDefault();

// modeles.cs et outils/formatage.cs sont compilés avec ce fichier, comme dans un projet.
List<Commande> commandes = JsonSerializer.Deserialize<List<Commande>>(Echantillon.Json)!;
var filtrees = ville is null ? commandes : commandes.Where(c => c.Ville.Equals(ville, StringComparison.OrdinalIgnoreCase)).ToList();

Console.WriteLine($"Rapport {(ville is null ? "toutes villes" : ville)} — {Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company}");
foreach (var groupe in filtrees.GroupBy(c => c.Ville).OrderBy(g => g.Key))
    Console.WriteLine($"  {groupe.Key,-10} {groupe.Count(),3} commande(s)  {Formatage.Euros(groupe.Sum(c => c.Montant)),12}");
Console.WriteLine($"  {"Total",-10} {filtrees.Count,3} commande(s)  {Formatage.Euros(filtrees.Sum(c => c.Montant)),12}");
