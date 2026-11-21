// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 15 · Nouveau modèle de sécurité mémoire (C# 15, PREVIEW)
// Documentation : docs/15-securite-memoire.md
//   dotnet run -c Release --project src/NouveautesDotNet11.SecuriteMemoire
//
// Ce projet est compilé avec les nouvelles règles (voir le .csproj). Le code « avant » (C# 14) est
// dans la documentation : sous les nouvelles règles, il ne compile plus tel quel (erreurs CS9360, CS9362).
// ═════════════════════════════════════════════════════════════════════════════════════════════════

using System.Reflection;

Console.OutputEncoding = System.Text.Encoding.UTF8;
int echecs = 0;
void Verifier(bool condition, string affirmation)
{
    Console.WriteLine($"     {(condition ? "✓" : "✗ ÉCHEC :")} {affirmation}");
    if (!condition) echecs++;
}

Console.WriteLine("── 15 · Nouveau modèle de sécurité mémoire (C# 15, preview) ──");

int[] mesures = [3, 1, 4, 1, 5, 9, 2, 6];

Console.WriteLine("\n   ▸ Pointeurs, & et fixed sans « unsafe » ; seul le déréférencement est marqué");
int somme = Calculs.Somme(mesures);
Console.WriteLine($"     somme = {somme}");
Verifier(somme == mesures.Sum(), "le seul bloc unsafe de la méthode entoure la lecture debut[i]");

Console.WriteLine("\n   ▸ unsafe(expression) : une seule expression dangereuse au milieu de code sûr");
byte premier = Calculs.PremierOctet(0x11223344);
Console.WriteLine($"     premier octet de 0x11223344 en mémoire : 0x{premier:X2}");
Verifier(premier == (BitConverter.IsLittleEndian ? 0x44 : 0x11), "l'ordre des octets en mémoire est lu à travers un pointeur");

Console.WriteLine("\n   ▸ Une méthode marquée unsafe : c'est désormais l'APPELANT qui doit l'assumer");
int[] tableau = [10, 20, 30];
int lu;
fixed (int* p = tableau)
{
    // Calculs.LireSansControle(p, 1);       ← erreur CS9362 : doit être appelé dans un contexte non sécurisé
    lu = unsafe(Calculs.LireSansControle(p, 1)); // l'appelant signe : « je garantis que 1 < 3 »
}
Console.WriteLine($"     LireSansControle(p, 1) = {lu}");
Verifier(lu == 20, "l'appel compile seulement à l'intérieur de unsafe(…)");

Console.WriteLine("\n   ▸ Ce que le compilateur inscrit dans le binaire");
var attribut = Assembly.GetExecutingAssembly().GetCustomAttributesData()
    .Concat(typeof(Calculs).Module.GetCustomAttributesData())
    .FirstOrDefault(a => a.AttributeType.Name == "MemorySafetyRulesAttribute");
Console.WriteLine($"     {(attribut is null ? "(aucun attribut)" : attribut.ToString())}");
Verifier(attribut is not null, "l'assembly déclare les règles avec lesquelles elle a été compilée");
var methode = typeof(Calculs).GetMethod(nameof(Calculs.LireSansControle))!;
string marques = string.Join(", ", methode.GetCustomAttributesData().Select(a => a.AttributeType.Name));
Console.WriteLine($"     attributs de LireSansControle : {(marques == "" ? "(aucun)" : marques)}");
Verifier(marques.Contains("RequiresUnsafeAttribute"), "une méthode unsafe porte [RequiresUnsafe] : les autres assemblies voient l'exigence");

Console.WriteLine();
Console.WriteLine(echecs == 0 ? "Toutes les vérifications sont passées." : $"{echecs} vérification(s) en échec.");
return echecs;

static class Calculs
{
    // Déclarer un pointeur, prendre une adresse, fixed, sizeof : plus besoin de contexte unsafe.
    // Seul le DÉRÉFÉRENCEMENT (*p, p[i], p->x) lit la mémoire sans contrôle : seul lui est marqué.
    public static int Somme(int[] valeurs)
    {
        int total = 0;
        fixed (int* debut = valeurs)
        {
            for (int i = 0; i < valeurs.Length; i++)
            {
                unsafe { total += debut[i]; }   // ← la seule ligne à relire avec attention
            }
        }
        return total;
    }

    public static byte PremierOctet(int valeur)
    {
        int* adresse = &valeur;                  // sûr : on ne fait que prendre une adresse
        byte* octet = (byte*)adresse;            // sûr : simple conversion de pointeur
        return unsafe(*octet);                   // dangereux : lecture mémoire, isolée dans unsafe(…)
    }

    /// <summary>Lit valeurs[index] sans vérifier les bornes.</summary>
    /// <remarks>Sécurité : l'appelant garantit que <paramref name="index"/> est dans le tableau pointé.</remarks>
    // « unsafe » sur un membre ne rend plus son corps unsafe : il signifie « exige un contexte unsafe chez
    // l'appelant », comme « unsafe fn » en Rust. Le corps doit marquer lui-même ses opérations dangereuses.
    public static unsafe int LireSansControle(int* valeurs, int index) => unsafe(valeurs[index]);
}
