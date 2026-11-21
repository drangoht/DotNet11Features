#!/usr/bin/env dotnet
// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 16 · File-based apps: #:include (.NET 11 SDK)
// Documentation: docs/en/16-file-based-apps.md · docs/fr/16-applications-fichier.md
//   dotnet run file-based-app/report.cs
//   dotnet run file-based-app/report.cs -- Lyon
// No .csproj: the #: directives at the top of the file replace the project.
// ═════════════════════════════════════════════════════════════════════════════════════════════════
#:include models.cs
#:include tools/formatting.cs
#:property Company="Coffee & Tea Shop"
#:property PublishAot=false

using System.Reflection;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;
string? city = args.FirstOrDefault();

// models.cs and tools/formatting.cs are compiled with this file, as in a project.
List<Order> orders = JsonSerializer.Deserialize<List<Order>>(Sample.Json)!;
var filtered = city is null ? orders : orders.Where(o => o.City.Equals(city, StringComparison.OrdinalIgnoreCase)).ToList();

Console.WriteLine($"Report {(city is null ? "all cities" : city)} — {Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company}");
foreach (var group in filtered.GroupBy(o => o.City).OrderBy(g => g.Key))
    Console.WriteLine($"  {group.Key,-10} {group.Count(),3} order(s)  {Formatting.Euros(group.Sum(o => o.Amount)),12}");
Console.WriteLine($"  {"Total",-10} {filtered.Count,3} order(s)  {Formatting.Euros(filtered.Sum(o => o.Amount)),12}");
