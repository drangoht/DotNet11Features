// Included by report.cs with #:include tools/formatting.cs (a relative path, sub-folder included)

using System.Globalization;

static class Formatting
{
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Euros(decimal amount) => amount.ToString("C", Fr);
}
