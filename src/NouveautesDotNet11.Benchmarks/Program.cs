using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using NouveautesDotNet11;
using NouveautesDotNet11.Exemples;

// Mesures rigoureuses des exemples où .NET 11 change les performances.
//   dotnet run -c Release --project src/NouveautesDotNet11.Benchmarks -- --filter *            (tout, ~15 min)
//   dotnet run -c Release --project src/NouveautesDotNet11.Benchmarks -- --filter *Flux* --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>Exemple 07 : compresser 4,6 Mo de JSON.</summary>
[MemoryDiagnoser]
public class CompressionBenchmarks
{
    private byte[] _json = [];

    [GlobalSetup] public void Preparer() => _json = Donnees.JsonCommandes(20_000);

    private byte[] Compresser(Func<Stream, Stream> creer)
    {
        using var sortie = new MemoryStream(_json.Length / 4);
        using (var flux = creer(sortie)) flux.Write(_json);
        return sortie.ToArray();
    }

    [Benchmark(Baseline = true)] public byte[] GZip_Optimal() => Compresser(s => new GZipStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Brotli_Optimal() => Compresser(s => new BrotliStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Zstandard_Optimal() => Compresser(s => new ZstandardStream(s, CompressionLevel.Optimal));
    [Benchmark] public byte[] Zstandard_Qualite9() => Compresser(s => new ZstandardStream(s, new ZstandardCompressionOptions { Quality = 9 }, leaveOpen: false));
}

/// <summary>Exemple 07 : 1 000 petits messages, avec et sans dictionnaire.</summary>
[MemoryDiagnoser]
public class DictionnaireBenchmarks
{
    private byte[][] _messages = [];
    private ZstandardDictionary _dictionnaire = null!;
    private readonly byte[] _tampon = new byte[64 * 1024];

    [GlobalSetup]
    public void Preparer()
    {
        var entrainement = Donnees.MessagesJson(2_000, graine: 1);
        _dictionnaire = ZstandardDictionary.Create(
            ZstandardDictionary.Train(entrainement.SelectMany(m => m).ToArray(), entrainement.Select(m => m.Length).ToArray(), 4096), quality: 3);
        _messages = Donnees.MessagesJson(1_000, graine: 2);
    }

    [GlobalCleanup] public void Nettoyer() => _dictionnaire.Dispose();

    [Benchmark(Baseline = true)]
    public long SansDictionnaire()
    {
        long total = 0;
        foreach (var m in _messages) { ZstandardEncoder.TryCompress(m, _tampon, out int n); total += n; }
        return total;
    }

    [Benchmark]
    public long AvecDictionnaire()
    {
        long total = 0;
        foreach (var m in _messages)
        {
            ZstandardEncoder.TryCompress(m, _tampon, out int n, _dictionnaire, ZstandardCompressionOptions.DefaultWindowLog2);
            total += n;
        }
        return total;
    }
}

/// <summary>Exemple 08 : jointure externe gauche, 10 000 clients × 30 000 commandes.</summary>
[MemoryDiagnoser]
public class JointuresBenchmarks
{
    private (int Id, string Nom)[] _clients = [];
    private (int ClientId, decimal Montant)[] _commandes = [];

    [GlobalSetup]
    public void Preparer()
    {
        var aleatoire = new Random(42);
        _clients = Enumerable.Range(1, 10_000).Select(i => (i, $"Client {i}")).ToArray();
        _commandes = Enumerable.Range(1, 30_000).Select(_ => (aleatoire.Next(1, 12_000), (decimal)aleatoire.Next(1, 500))).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int Avant_GroupJoin_SelectMany() => _clients
        .GroupJoin(_commandes, c => c.Id, o => o.ClientId, (c, os) => (c, os))
        .SelectMany(x => x.os.DefaultIfEmpty(), (x, o) => (x.c.Nom, o.Montant))
        .Count();

    [Benchmark]
    public int Apres_LeftJoin() => _clients
        .LeftJoin(_commandes, c => c.Id, o => o.ClientId, (c, o) => (c.Nom, o.Montant))
        .Count();

    [Benchmark]
    public int Avant_FullJoin_Manuel()
    {
        var idsClients = _clients.Select(c => c.Id).ToHashSet();
        return _clients
            .GroupJoin(_commandes, c => c.Id, o => o.ClientId, (c, os) => (c, os))
            .SelectMany(x => x.os.DefaultIfEmpty(), (x, o) => (x.c.Id, o.Montant))
            .Concat(_commandes.Where(o => !idsClients.Contains(o.ClientId)).Select(o => (o.ClientId, o.Montant)))
            .Count();
    }

    [Benchmark]
    public int Apres_FullJoin() => _clients.FullJoin(_commandes, c => c.Id, o => o.ClientId).Count();
}

/// <summary>Exemple 09 : désérialiser une chaîne JSON de 1,2 Mo à travers un Stream.</summary>
[MemoryDiagnoser]
public class FluxBenchmarks
{
    private string _json = "";

    [GlobalSetup] public void Preparer() => _json = Donnees.Texte(Donnees.JsonCommandes(5_000));

    [Benchmark(Baseline = true)]
    public int Avant_MemoryStream_GetBytes()
    {
        using var flux = new MemoryStream(Encoding.UTF8.GetBytes(_json));
        return JsonSerializer.Deserialize<List<Donnees.Commande>>(flux)!.Count;
    }

    [Benchmark]
    public int Apres_StringStream()
    {
        using var flux = new StringStream(_json, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<Donnees.Commande>>(flux)!.Count;
    }
}

/// <summary>Exemple 11 : écrire 10 000 événements en JSON Lines.</summary>
[MemoryDiagnoser]
public class JsonLinesBenchmarks
{
    private static async IAsyncEnumerable<E11_JsonLines.Evenement> Source()
    {
        for (int i = 0; i < 10_000; i++) yield return new E11_JsonLines.Evenement(i, "payee", "CMD-0001");
        await Task.CompletedTask;
    }

    [Benchmark(Baseline = true)]
    public async Task<long> Avant_BoucleManuelle()
    {
        using var sortie = new MemoryStream();
        byte[] saut = "\n"u8.ToArray();
        await foreach (var e in Source())
        {
            await JsonSerializer.SerializeAsync(sortie, e);
            await sortie.WriteAsync(saut);
        }
        return sortie.Length;
    }

    [Benchmark]
    public async Task<long> Apres_SerializeAsyncEnumerable()
    {
        using var sortie = new MemoryStream();
        await JsonSerializer.SerializeAsyncEnumerable(sortie, Source(), topLevelValues: true);
        return sortie.Length;
    }
}

/// <summary>Exemple 01 : le coût d'une union par rapport à une hiérarchie de classes.</summary>
[MemoryDiagnoser]
public class UnionsBenchmarks
{
    private object[] _objets = [];
    private MoyenPaiement[] _unions = [];

    [GlobalSetup]
    public void Preparer()
    {
        _objets = Enumerable.Range(0, 1_000)
            .Select(i => (i % 3) switch { 0 => (object)new Carte("4242"), 1 => new Virement("FR76"), _ => new BonAchat("B", 1) }).ToArray();
        _unions = _objets.Select(o => o switch { Carte c => (MoyenPaiement)c, Virement v => v, _ => (BonAchat)o }).ToArray();
    }

    // Avant : les mêmes objets rangés dans un object[], switch avec bras par défaut.
    [Benchmark(Baseline = true)]
    public long Avant_Object_Switch()
    {
        long total = 0;
        foreach (object p in _objets)
            total += p switch { Carte c => c.Derniers4Chiffres.Length, Virement => 2, BonAchat => 3, _ => throw new InvalidOperationException() };
        return total;
    }

    // Après : une union (struct qui enveloppe la référence), switch exhaustif.
    [Benchmark]
    public long Apres_Union_Switch()
    {
        long total = 0;
        foreach (MoyenPaiement m in _unions)
            total += m switch { Carte c => c.Derniers4Chiffres.Length, Virement => 2, BonAchat => 3 };
        return total;
    }
}

/// <summary>Exemple 01 : le coût du boxing quand une union contient un type valeur.</summary>
[MemoryDiagnoser]
public class UnionsBoxingBenchmarks
{
    private readonly int[] _valeurs = Enumerable.Range(0, 1_000).ToArray();

    [Benchmark(Baseline = true)]
    public long Int_Sans_Union()
    {
        long total = 0;
        foreach (int v in _valeurs) total += v;
        return total;
    }

    [Benchmark]
    public long Int_Dans_Une_Union()
    {
        long total = 0;
        foreach (int v in _valeurs)
        {
            IdentifiantClient id = v;   // boxé : 24 octets
            total += id switch { int n => n, Guid => 1, string s => s.Length };
        }
        return total;
    }
}

/// <summary>Exemple 14 : un comparateur créé par EqualityComparer.Create contre une classe écrite à la main.</summary>
[MemoryDiagnoser]
public class ComparateursBenchmarks
{
    private Contact[] _contacts = [];
    private readonly IEqualityComparer<Contact> _classe = new ContactParEmailAvant();
    private readonly IEqualityComparer<Contact> _cree = EqualityComparer<Contact>.Create(c => c?.Email, StringComparer.OrdinalIgnoreCase);

    [GlobalSetup]
    public void Preparer() => _contacts = Enumerable.Range(0, 10_000)
        .Select(i => new Contact($"contact{i % 4_000}@exemple.fr", "Nom", "source")).ToArray();

    [Benchmark(Baseline = true)] public int Avant_ClasseIEqualityComparer() => _contacts.Distinct(_classe).Count();
    [Benchmark] public int Apres_EqualityComparerCreate() => _contacts.Distinct(_cree).Count();
}
