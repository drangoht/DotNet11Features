// Inclus par rapport.cs avec #:include outils/formatage.cs (un chemin relatif, sous-dossier compris)

using System.Globalization;

static class Formatage
{
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Euros(decimal montant) => montant.ToString("C", Fr);
}
