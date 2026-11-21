# 13 · Texte et Unicode : `Rune` dans `string`, `Utf16.IsValid`, `Base64`, `Regex AnyNewLine`, `StringBuilder.MoveChunks` (.NET 11)

🇬🇧 [English version](../en/13-text.md)

> **En une phrase** : une série de petites API qui suppriment des contournements quotidiens autour du texte.
>
> Code : [`E13_Text.cs`](../../src/DotNet11Features.Examples/Examples/E13_Text.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- text`

---

## 1. `string` accepte des `Rune`

### L'idée expliquée simplement

En .NET, un `char` occupe 16 bits. C'est assez pour « é » ou « € », mais **pas pour 🍵** (U+1F375) : cet emoji
occupe **deux `char`**, une « paire de substitution ». Un `char` seul ne peut donc pas le représenter.

`System.Text.Rune` (depuis .NET Core 3.0) représente **un caractère Unicode complet**, emoji compris. Mais `string`
ne savait pas chercher une `Rune` : il fallait la reconvertir en `string` à chaque fois.

| Avant | .NET 11 |
|---|---|
| `review.Contains(tea.ToString())` | `review.Contains(tea)` |
| `review.Replace(tea.ToString(), "☕")` | `review.Replace(tea, new Rune('☕'))` |
| `review.Split(tea.ToString())` | `review.Split(tea, StringSplitOptions.TrimEntries)` |

Surcharges ajoutées : `Contains`, `StartsWith`, `EndsWith`, `IndexOf`, `LastIndexOf` (avec ou sans
`StringComparison`), `Replace`, `Split`, `Trim`, `TrimStart`, `TrimEnd`, plus `TextInfo.ToUpper/ToLower(Rune)`.
Et `char.Equals('É', 'é', StringComparison.CurrentCultureIgnoreCase)` pour comparer deux `char` selon une règle.

---

## 2. `Utf16.IsValid` : repérer une chaîne coupée en deux

Couper une chaîne au mauvais endroit (`text[..9]`, une troncature de base de données, un découpage de message)
peut **séparer les deux moitiés d'un emoji**. La chaîne obtenue est invalide : elle s'affiche avec des « � » et
certains encodeurs lèvent une exception.

```csharp
string truncated = review[..9];                         // coupe 🍵 en deux
Utf16.IsValid(truncated)                                // false
Utf16.IndexOfInvalidSubsequence(truncated)              // 8 : la moitié orpheline
```

Avant, il fallait parcourir les `char` en testant `char.IsHighSurrogate` / `IsLowSurrogate`. Les deux méthodes sont
vectorisées (SIMD). Même chose pour l'UTF-8 avec `Utf8.IndexOfInvalidSubsequence`.

---

## 3. `Base64` sait produire des chaînes

Il y avait **deux** API Base64 en .NET :

- `Convert.ToBase64String` / `FromBase64String` : simples, mais pensées pour `byte[]` et `string` ;
- `System.Buffers.Text.Base64` : très rapide, sur des spans, mais **uniquement en UTF-8** (`byte` vers `byte`).

.NET 11 complète la seconde avec des versions `char` et `string` : `EncodeToString`, `EncodeToChars`,
`TryEncodeToChars`, `DecodeFromChars`, `TryDecodeFromChars`… Une seule API pour tous les cas, y compris l'écriture
dans un tampon sur la pile, sans aucune allocation :

```csharp
Span<char> buffer = stackalloc char[16];
Base64.TryEncodeToChars(key, buffer, out int written);   // zéro allocation
```

---

## 4. `RegexOptions.AnyNewLine`

En .NET, `^` et `$` en mode `Multiline` ne reconnaissent que `\n`. Un fichier Windows (`\r\n`) fait échouer `$` :
juste avant `\n` se trouve un `\r`, pas la fin de la ligne attendue.

```csharp
string log = "line1\r\nline2\rline3 line4";
Regex.Matches(log, @"^line\d$", RegexOptions.Multiline).Count                            // 0 (vérifié aussi en .NET 10)
Regex.Matches(log, @"^line\d$", RegexOptions.Multiline | RegexOptions.AnyNewLine).Count  // 4
```

Avec `AnyNewLine`, `^`, `$` et `.` traitent **tous** les sauts de ligne Unicode comme des fins de ligne (l'exemple
vérifie `\r\n`, `\r`, `\n` et U+2028). Le contournement habituel (`\r?$`) ne couvrait que le cas Windows.

---

## 5. `StringBuilder.MoveChunks`

Un `StringBuilder` stocke son texte en **morceaux** chaînés. Pour transférer son contenu vers un autre
`StringBuilder` (le confier à un autre composant, réinitialiser un tampon réutilisé…), on faisait
`new StringBuilder(source.ToString())` : une chaîne complète, puis une deuxième copie.

```csharp
var moved = StringBuilder.MoveChunks(source);   // la source est vidée, ses morceaux sont déplacés tels quels
```

---

## 6. Sortie de l'exemple

```
   ▸ After: Contains, IndexOf, Replace, Split, Trim… take a Rune directly
     review.Contains(tea) = True · review.IndexOf(tea) = 8
     review.Replace(tea, new Rune('☕')) = Service ☕ perfect ☕ recommended
     review.Split(tea, TrimEntries) = [Service | perfect | recommended]
   ▸ Utf16.IsValid: spot a string cut in the middle of an emoji
     "Service 🍵 "[..9] → IsValid = False, first invalid character at index 8
   ▸ Base64: the high-performance class (System.Buffers.Text) can now produce strings
     Convert.ToBase64String = 3q2+7yAm · Base64.EncodeToString = 3q2+7yAm
     Base64.TryEncodeToChars into a stack buffer: 3q2+7yAm (zero allocation)
   ▸ Regex: RegexOptions.AnyNewLine recognizes \r\n, \r, \n,  …
     ^line\d$ in Multiline: 0 match(es); with AnyNewLine: 4
   ▸ StringBuilder.MoveChunks: transfer the content without copying it
     copy through ToString: 542 KB allocated · MoveChunks: 48 bytes
```

## 7. Pièges et limites

1. **`IndexOf(Rune)` renvoie un index en `char`**, pas en Rune : 8 dans l'exemple, la position du premier des deux
   `char` de l'emoji. C'est cohérent avec `Substring` et les plages `[..]`.
2. **`AnyNewLine` change aussi `.`**, qui ne reconnaît plus aucun saut de ligne (sauf avec `Singleline`).
3. **`MoveChunks` vide la source** : elle reste utilisable, mais sa `Length` vaut 0.

## Sources

- [What's new in .NET 11 libraries — Strings, Base64, Unicode, Regex](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
