using System.Diagnostics;
using System.Text;
using static NouveautesDotNet11.Affichage;

namespace NouveautesDotNet11.Exemples;

// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 06 · Lancer une commande et récupérer sa sortie en une ligne : Process.RunAndCaptureTextAsync
// Documentation : docs/06-process.md
// ═════════════════════════════════════════════════════════════════════════════════════════════════

public static class E06_Process
{
    // ── Avant : la version correcte demande une vingtaine de lignes ─────────────────────────────
    // Le piège classique : lire StandardOutput.ReadToEnd() PUIS StandardError.ReadToEnd() peut bloquer
    // à jamais si le programme remplit le tampon d'erreur (~4 Ko) pendant qu'on attend la sortie standard.
    // Il faut donc lire les deux flux en parallèle.
    static async Task<(int Code, string Sortie, string Erreurs)> LancerAvantAsync(string programme, params string[] arguments)
    {
        var info = new ProcessStartInfo(programme)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in arguments) info.ArgumentList.Add(a);

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Impossible de lancer {programme}");
        Task<string> sortie = process.StandardOutput.ReadToEndAsync();   // les deux lectures en même temps
        Task<string> erreurs = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await sortie, await erreurs);
    }

    public static async Task Executer()
    {
        Titre("06 · Process.RunAndCaptureTextAsync (.NET 11)");

        Section("Avant : ProcessStartInfo + redirections + lecture parallèle des deux flux");
        var (code, sortie, _) = await LancerAvantAsync("dotnet", "--version");
        Ligne($"code {code} · dotnet --version → {sortie.Trim()}");

        Section("Après : une ligne");
        ProcessTextOutput resultat = await Process.RunAndCaptureTextAsync("dotnet", ["--version"]);
        Ligne($"code {resultat.ExitStatus.ExitCode} · dotnet --version → {resultat.StandardOutput.Trim()} (pid {resultat.ProcessId})");
        Verifier(resultat.ExitStatus.ExitCode == 0 && resultat.StandardOutput.Trim() == sortie.Trim(), "même résultat que la version d'avant");

        Section("La sortie d'erreur est capturée à part, avec le code de retour");
        ProcessTextOutput echec = await Process.RunAndCaptureTextAsync("dotnet", ["commande-qui-n-existe-pas"]);
        string premiereLigne = (echec.StandardError + echec.StandardOutput).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        Ligne($"code {echec.ExitStatus.ExitCode} · {premiereLigne[..Math.Min(90, premiereLigne.Length)]}");
        Verifier(echec.ExitStatus.ExitCode != 0, "un échec ne lève pas d'exception : on lit ExitStatus.ExitCode");

        Section("Annulation : le processus est arrêté, ExitStatus.Canceled vaut true");
        string[] attente = OperatingSystem.IsWindows()
            ? ["powershell", "-NoProfile", "-Command", "Start-Sleep -Seconds 30"]
            : ["sleep", "30"];
        using var delai = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var chrono = Stopwatch.StartNew();
        try
        {
            ProcessTextOutput annule = await Process.RunAndCaptureTextAsync(attente[0], attente[1..], delai.Token);
            Ligne($"terminé en {chrono.ElapsedMilliseconds} ms · Canceled = {annule.ExitStatus.Canceled}");
            Verifier(annule.ExitStatus.Canceled, "le processus de 30 s est arrêté au bout de 0,5 s");
        }
        catch (OperationCanceledException)
        {
            Ligne($"OperationCanceledException au bout de {chrono.ElapsedMilliseconds} ms");
            Verifier(chrono.ElapsedMilliseconds < 10_000, "le processus de 30 s est arrêté sans attendre sa fin");
        }

        Section("Version synchrone avec délai : Process.Run");
        ProcessExitStatus statut = Process.Run("dotnet", ["--info"], silent: true, timeout: TimeSpan.FromSeconds(30));
        Ligne($"dotnet --info (sortie ignorée, silent: true) → code {statut.ExitCode}");
        Verifier(statut.ExitCode == 0, "Process.Run renvoie directement le statut de sortie");
    }
}
