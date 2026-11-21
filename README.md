# NouveautesDotNet11 — les nouveautés de .NET 11 et C# 15, exemple par exemple

Seize exemples exécutables, chacun avec **le code d'avant**, **le code .NET 11**, ce qui change vraiment, des
mesures quand c'est pertinent, et les pièges rencontrés en l'écrivant.

Testé avec le SDK `11.0.100`, la version finale de .NET 11 sortie le 10 novembre 2026, épinglé dans `global.json`.
C# 15 est la version par défaut pour `net11.0` : aucun réglage à ajouter, sauf pour l'exemple 15 (preview).

> Le **Runtime Async**, la plus grosse nouveauté du runtime, a son propre dépôt :
> [RuntimeAsyncDemo](https://github.com/drangoht/RuntimeAsyncDemo) (machine à états contre runtime, benchmarks,
> pile d'appels). Ce dépôt-ci couvre **tout le reste**.

**Chaque affirmation est vérifiée à l'exécution** : les exemples affichent `✓` ou `✗` et le programme se termine avec
le nombre d'échecs. `./verifier.ps1` lance tout.

---

## 1. Sommaire

### Langage : C# 15

| # | Nouveauté | Avant | Après | Doc |
|---|---|---|---|---|
| 01 | **Unions** | classe enveloppe ou `OneOf<…>`, bras `_ => throw` | `union MoyenPaiement(Carte, Virement, BonAchat);`, `switch` vérifié | [01-unions](docs/01-unions.md) |
| 02 | **Hiérarchies fermées** | `abstract` + `_ => throw new UnreachableException()` | `closed record EtatCommande;` | [02-hierarchies-fermees](docs/02-hierarchies-fermees.md) |
| 03 | **Arguments de collection** | `new HashSet<string>(comparateur)` + boucle | `[with(StringComparer.OrdinalIgnoreCase), .. items]` | [03-arguments-collection](docs/03-arguments-collection.md) |
| 04 | **Indexeurs d'extension** | `json.GetProperty("a").GetProperty("b")` | `json["a"]["b"]` | [04-indexeurs-extension](docs/04-indexeurs-extension.md) |
| 05 | **`break` / `continue` étiquetés** | drapeau booléen ou `goto` | `allees: for (…) { … break allees; }` | [05-break-continue-etiquetes](docs/05-break-continue-etiquetes.md) |
| 15 | **Sécurité mémoire** *(preview)* | méthode entière `unsafe` | `unsafe(*p)` sur la seule expression dangereuse | [15-securite-memoire](docs/15-securite-memoire.md) |

### Bibliothèques

| # | Nouveauté | Avant | Après | Doc |
|---|---|---|---|---|
| 06 | **`Process.RunAndCaptureTextAsync`** | ~14 lignes et un piège d'interblocage | `await Process.RunAndCaptureTextAsync("git", ["status"])` | [06-process](docs/06-process.md) |
| 07 | **Zstandard intégré** | paquet tiers + DLL native | `new ZstandardStream(…)`, dictionnaires entraînés | [07-zstandard](docs/07-zstandard.md) |
| 08 | **LINQ `FullJoin`** et jointures sans sélecteur | GroupJoin + SelectMany + DefaultIfEmpty + Concat | `catalogue.FullJoin(stock, a => a.Sku, s => s.Sku)` | [08-jointures-linq](docs/08-jointures-linq.md) |
| 09 | **`StringStream`, `ReadOnlyMemoryStream`…** | `new MemoryStream(Encoding.UTF8.GetBytes(s))` | `new StringStream(s, Encoding.UTF8)`, sans copie | [09-flux](docs/09-flux.md) |
| 10 | **JSON : unions, hiérarchies fermées, nommage par type** | `JsonConverter` maison, liste de `[JsonDerivedType]` | `[JsonUnion(TypeClassifier = …)]`, `InferClosedTypePolymorphism` | [10-json-unions](docs/10-json-unions.md) |
| 11 | **JSON Lines** | boucle `SerializeAsync` + `\n` | `SerializeAsyncEnumerable(…, topLevelValues: true)` | [11-json-lines](docs/11-json-lines.md) |
| 12 | **Nombres** | `0.1 + 0.2 ≠ 0.3`, découpage manuel, `NextBytes` | `Decimal64`, `TryParsePartial`, `NextInteger<T>` | [12-nombres](docs/12-nombres.md) |
| 13 | **Texte et Unicode** | `Contains(rune.ToString())`, `\r?$` | `Contains(rune)`, `Utf16.IsValid`, `RegexOptions.AnyNewLine` | [13-texte](docs/13-texte.md) |
| 14 | **`EqualityComparer<T>.Create(clé)`, `BitArray`** | une classe `IEqualityComparer<T>` | `EqualityComparer<Contact>.Create(c => c?.Email)` | [14-comparateurs-bitarray](docs/14-comparateurs-bitarray.md) |

### SDK

| # | Nouveauté | Avant | Après | Doc |
|---|---|---|---|---|
| 16 | **`#:include` dans les applications en un fichier** | un seul fichier, ou conversion en projet | `#:include modeles.cs` | [16-applications-fichier](docs/16-applications-fichier.md) |

### Comment chaque fiche est construite

1. **L'idée expliquée simplement**, avec une image du quotidien quand c'est utile ;
2. **Comment on faisait avant**, le vrai code, avec ses défauts ;
3. **Ce que change .NET 11**, avec un schéma Mermaid quand le mécanisme s'y prête ;
4. **Côte à côte** avant / après ;
5. **Résultats** : la sortie réelle de l'exemple et, quand c'est pertinent, des benchmarks ;
6. **Pièges et limites**, dont ceux rencontrés en écrivant le code.

---

## 2. Ce qu'il faut retenir

- **Les unions et les hiérarchies fermées** sont la nouveauté qui changera le plus la façon d'écrire du C# : le
  compilateur vérifie enfin qu'un `switch` traite tous les cas. Passez **CS8509 en erreur** pour en profiter
  (voir `Directory.Build.props`).
- **Une union de types valeur boxe** (24 octets pour un `int`). Une union de classes ne coûte rien de plus qu'un
  `object`.
- **Un changement de comportement à tester avant de migrer** : `(decimal)1.23` vaut maintenant
  `1.2299999999999999822364316060` au lieu de `1.23` ([exemple 12](docs/12-nombres.md#2-️-changement-de-comportement--decimalundouble)).
- **Zstandard n'est pas magique** face au GZip de .NET (déjà rapide grâce à zlib-ng) : son niveau par défaut est
  5× plus rapide mais produit des fichiers plus gros. Son vrai atout est le **dictionnaire** pour les petits messages
  (×4 en taille, ×1,8 en vitesse).
- **Les nouveaux flux** (`StringStream`…) suppriment des copies entières de données : −1,2 Mo et −27 % de temps pour
  désérialiser 1,2 Mo de JSON depuis une chaîne.
- **Plusieurs nouveautés sont surtout de la lisibilité**, sans gain de performance mesurable :
  `EqualityComparer<T>.Create`, indexeurs d'extension, `break` étiqueté. `FullJoin` est même plus lent qu'une jointure
  manuelle bien écrite.

---

## 3. Lancer

```bash
# Tout vérifier (exemples 01-14, puis 15 et 16)
./verifier.ps1

# Les exemples 01 à 14, tous ou un seul (par numéro ou par nom)
dotnet run -c Release --project src/NouveautesDotNet11.Exemples
dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- unions
dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- 7
dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- liste

# Exemple 15 (C# 15 preview, projet séparé)
dotnet run -c Release --project src/NouveautesDotNet11.SecuriteMemoire

# Exemple 16 (application en un fichier, sans projet)
dotnet run appli-fichier/rapport.cs -- Lyon

# Benchmarks (BenchmarkDotNet) : tous en job court (~6 min), ou une seule classe
dotnet run -c Release --project src/NouveautesDotNet11.Benchmarks -- --filter * --job short
dotnet run -c Release --project src/NouveautesDotNet11.Benchmarks -- --filter *Flux*
```

---

## 4. Résultats mesurés

BenchmarkDotNet 0.16.0-preview.2, `--job short` (3 itérations), SDK .NET 11.0.100 / runtime 11.0.0, AMD Ryzen 7
9800X3D, Windows 11, mesuré le 2026-11-21. Le job court donne des ordres de grandeur fiables mais des marges d'erreur
larges : relancez sans `--job short` pour des chiffres définitifs.

### Unions (exemple 01), 1 000 valeurs

| Méthode | Temps | Mémoire |
|---|---:|---:|
| `object[]` + `switch` avec `_ => throw` | 1,79 µs | 0 |
| union de classes + `switch` exhaustif | 1,78 µs | 0 |
| `int` seul | 0,21 µs | 0 |
| `int` rangé dans une union | 1,35 µs | 24 000 o |

### Zstandard (exemple 07)

| Compression de 4,6 Mo de JSON | Temps | Taille (démo) |
|---|---:|---:|
| GZip (Optimal) | 21,3 ms | 340 Ko |
| Brotli (Optimal) | 14,7 ms | 393 Ko |
| Zstandard (Optimal) | **4,0 ms** | 438 Ko |
| Zstandard (qualité 9) | 29,5 ms | 320 Ko |

| 1 000 messages de 240 octets | Temps | Taille moyenne |
|---|---:|---:|
| Zstandard sans dictionnaire | 4,07 ms | 166 o |
| Zstandard avec dictionnaire | **2,21 ms** | **45 o** |

### Jointures LINQ (exemple 08), 10 000 clients × 30 000 commandes

| Méthode | Temps | Mémoire |
|---|---:|---:|
| GroupJoin + SelectMany + DefaultIfEmpty | 2,35 ms | 3,96 Mo |
| LeftJoin (.NET 10) | 2,36 ms | 2,84 Mo |
| Jointure complète manuelle (avec `HashSet`) | 2,62 ms | 4,48 Mo |
| FullJoin (.NET 11) | 4,83 ms | 3,49 Mo |

### Flux (exemple 09) : désérialiser 1,2 Mo de JSON depuis une chaîne

| Méthode | Temps | Mémoire |
|---|---:|---:|
| `MemoryStream(Encoding.UTF8.GetBytes(json))` | 5,69 ms | 5,47 Mo |
| `StringStream(json, Encoding.UTF8)` | **4,13 ms** | **4,31 Mo** |

### JSON Lines (exemple 11) : 10 000 événements

| Méthode | Temps | Mémoire |
|---|---:|---:|
| boucle `SerializeAsync` + `\n` | 1,48 ms | 4,21 Mo |
| `SerializeAsyncEnumerable(…, topLevelValues: true)` | **1,22 ms** | **2,38 Mo** |

### Comparateurs (exemple 14) : `Distinct` sur 10 000 contacts

| Méthode | Temps | Mémoire |
|---|---:|---:|
| classe `IEqualityComparer<Contact>` | 262 µs | 197,6 Ko |
| `EqualityComparer<Contact>.Create(c => c?.Email, …)` | 280 µs (±24) | 197,6 Ko |

---

## 5. Non couvert ici

- **Runtime Async** : voir [RuntimeAsyncDemo](https://github.com/drangoht/RuntimeAsyncDemo).
- **JIT et runtime** : élimination de vérifications de bornes, dévirtualisation, AVX-VNNI-512, rapports de plantage
  en processus sous Linux, plancher matériel x86-64-v2. Gains automatiques, sans code à écrire.
- **ASP.NET Core 11, EF Core 11, .NET MAUI** (CoreCLR par défaut) : hors du périmètre de ce dépôt.
- **Autres API** non illustrées : `Process.Signal`, `SafeProcessHandle`, `Dns.ResolveSrv/Mx/Txt`, `X25519DiffieHellman`,
  `Aes.EncryptKeyWrap`, validation asynchrone (`AsyncValidationAttribute`), métriques de `MemoryCache`, mots de passe
  ZIP, format GNU pour `TarFile`, `CborReader` incrémental, vecteurs SIMD (`Vector128.Zip`…).

---

## Structure

```
NouveautesDotNet11/
├── Directory.Build.props        net11.0 pour tous les projets, CS8509 (switch incomplet) passé en erreur
├── global.json                  SDK .NET 11 (11.0.100, version finale)
├── verifier.ps1                 Lance tous les exemples, s'arrête au premier échec
├── docs/                        Une fiche par exemple (01 à 16)
├── appli-fichier/               Exemple 16 : application en un fichier avec #:include
│   ├── rapport.cs
│   ├── modeles.cs
│   └── outils/formatage.cs
└── src/
    ├── NouveautesDotNet11.Exemples/          Exemples 01 à 14, lançables un par un
    │   ├── Program.cs                        Sommaire et choix de l'exemple
    │   ├── Affichage.cs                      Titres, ✓ / ✗, compteur d'échecs
    │   ├── Donnees.cs                        Jeu de données reproductible (commandes, catalogue)
    │   └── Exemples/E01_Unions.cs … E14_Comparateurs.cs
    ├── NouveautesDotNet11.SecuriteMemoire/   Exemple 15 (LangVersion preview, projet isolé)
    └── NouveautesDotNet11.Benchmarks/        BenchmarkDotNet 0.16 preview (la 0.15.8 ne reconnaît pas .NET 11)
```

## Sources

- [What's new in .NET 11](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview) ·
  [bibliothèques](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries) ·
  [runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime) ·
  [SDK](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/sdk)
- [What's new in C# 15](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)

Licence MIT.
