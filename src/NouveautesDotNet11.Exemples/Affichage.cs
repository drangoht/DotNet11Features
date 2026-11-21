namespace NouveautesDotNet11;

/// <summary>
/// Petits utilitaires d'affichage partagés par tous les exemples.
/// <see cref="Verifier"/> transforme chaque affirmation de la documentation en contrôle : si .NET 11 ne se
/// comporte pas comme annoncé, la démo le signale et se termine avec un code d'erreur.
/// </summary>
public static class Affichage
{
    public static int Echecs { get; private set; }

    public static void Titre(string texte)
    {
        Console.WriteLine();
        Console.WriteLine($"── {texte} " + new string('─', Math.Max(3, 100 - texte.Length)));
    }

    public static void Section(string texte) => Console.WriteLine($"\n   ▸ {texte}");

    public static void Ligne(string texte = "") => Console.WriteLine("     " + texte);

    public static void Verifier(bool condition, string affirmation)
    {
        Console.WriteLine($"     {(condition ? "✓" : "✗ ÉCHEC :")} {affirmation}");
        if (!condition) Echecs++;
    }
}
