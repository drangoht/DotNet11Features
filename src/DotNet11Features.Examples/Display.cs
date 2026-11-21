namespace DotNet11Features;

/// <summary>
/// Small output helpers shared by every example.
/// <see cref="Check"/> turns each claim of the documentation into a runtime check: if .NET 11 does not behave
/// as documented, the demo says so and exits with an error code.
/// </summary>
public static class Display
{
    public static int Failures { get; private set; }

    public static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"── {text} " + new string('─', Math.Max(3, 100 - text.Length)));
    }

    public static void Section(string text) => Console.WriteLine($"\n   ▸ {text}");

    public static void Line(string text = "") => Console.WriteLine("     " + text);

    public static void Check(bool condition, string claim)
    {
        Console.WriteLine($"     {(condition ? "✓" : "✗ FAILED:")} {claim}");
        if (!condition) Failures++;
    }
}
