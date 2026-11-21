# 13 · Texte et Unicode : `Rune` dans `string`, `Utf16.IsValid`, `Base64`, `Regex AnyNewLine`, `StringBuilder.MoveChunks` (.NET 11)

> **En une phrase** : une série de petites API qui suppriment des contournements quotidiens autour du texte.
>
> Code : [`E13_Texte.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E13_Texte.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- texte`

---

## 1. `string` accepte des `Rune`

### L'idée expliquée simplement

En .NET, un `char` occupe 16 bits. C'est assez pour « é » ou « € », mais **pas pour 🍵** (U+1F375) : cet emoji
occupe **deux `char`**, une « paire de substitution ». Un `char` seul ne peut donc pas le représenter.

`System.Text.Rune` (depuis .NET Core 3.0) représente **un caractère Unicode complet**, emoji compris. Mais `string`
ne savait pas chercher une `Rune` : il fallait la reconvertir en `string` à chaque fois.

| Avant | .NET 11 |
|---|---|
| `avis.Contains(the.ToString())` | `avis.Contains(the)` |
| `avis.Replace(the.ToString(), "☕")` | `avis.Replace(the, new Rune('☕'))` |
| `avis.Split(the.ToString())` | `avis.Split(the, StringSplitOptions.TrimEntries)` |

Surcharges ajoutées : `Contains`, `StartsWith`, `EndsWith`, `IndexOf`, `LastIndexOf` (avec ou sans
`StringComparison`), `Replace`, `Split`, `Trim`, `TrimStart`, `TrimEnd`, plus `TextInfo.ToUpper/ToLower(Rune)`.
Et `char.Equals('É', 'é', StringComparison.CurrentCultureIgnoreCase)` pour comparer deux `char` selon une règle.

---

## 2. `Utf16.IsValid` : repérer une chaîne coupée en deux

Couper une chaîne au mauvais endroit (`texte[..9]`, une troncature de base de données, un découpage de message)
peut **séparer les deux moitiés d'un emoji**. La chaîne obtenue est invalide : elle s'affiche avec des « � » et
certains encodeurs lèvent une exception.

```csharp
string tronque = avis[..9];                             // coupe 🍵 en deux
Utf16.IsValid(tronque)                                  // false
Utf16.IndexOfInvalidSubsequence(tronque)                // 8 : la moitié orpheline
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
Span<char> tampon = stackalloc char[16];
Base64.TryEncodeToChars(cle, tampon, out int ecrits);   // zéro allocation
```

---

## 4. `RegexOptions.AnyNewLine`

En .NET, `^` et `$` en mode `Multiline` ne reconnaissent que `\n`. Un fichier Windows (`\r\n`) fait échouer `$` :
juste avant `\n` se trouve un `\r`, pas la fin de la ligne attendue.

```csharp
string journal = "ligne1\r\nligne2\rligne3 ligne4";
Regex.Matches(journal, @"^ligne\d$", RegexOptions.Multiline).Count                            // 0 (vérifié aussi en .NET 10)
Regex.Matches(journal, @"^ligne\d$", RegexOptions.Multiline | RegexOptions.AnyNewLine).Count  // 4
```

Avec `AnyNewLine`, `^`, `$` et `.` traitent **tous** les sauts de ligne Unicode comme des fins de ligne (l'exemple
vérifie `\r\n`, `\r`, `\n` et U+2028). Le contournement habituel (`\r?$`) ne couvrait que le cas Windows.

---

## 5. `StringBuilder.MoveChunks`

Un `StringBuilder` stocke son texte en **morceaux** chaînés. Pour transférer son contenu vers un autre
`StringBuilder` (le confier à un autre composant, réinitialiser un tampon réutilisé…), on faisait
`new StringBuilder(source.ToString())` : une chaîne complète, puis une deuxième copie.

```csharp
var deplace = StringBuilder.MoveChunks(source);   // la source est vidée, ses morceaux sont déplacés tels quels
```

---

## 6. Résultats de l'exemple

```
   ▸ Après : Contains, IndexOf, Replace, Split, Trim… acceptent directement une Rune
     avis.Contains(the) = True · avis.IndexOf(the) = 8
     avis.Replace(the, new Rune('☕')) = Service ☕ parfait ☕ je recommande
     avis.Split(the, TrimEntries) = [Service | parfait | je recommande]
   ▸ Utf16.IsValid : repérer une chaîne coupée au milieu d'un emoji
     "Service 🍵 "[..9] → IsValid = False, premier caractère invalide à l'index 8
   ▸ Base64 : la classe performante (System.Buffers.Text) sait maintenant produire des chaînes
     Convert.ToBase64String = 3q2+7yAm · Base64.EncodeToString = 3q2+7yAm
     Base64.TryEncodeToChars dans un tampon sur la pile : 3q2+7yAm (zéro allocation)
   ▸ Regex : RegexOptions.AnyNewLine reconnaît \r\n, \r, \n,  …
     ^ligne\d$ en Multiline : 0 correspondance(s) ; avec AnyNewLine : 4
   ▸ StringBuilder.MoveChunks : transférer le contenu sans le copier
     copie via ToString : 855 Ko alloués · MoveChunks : 48 octets
```

## 7. Pièges et limites

1. **`IndexOf(Rune)` renvoie un index en `char`**, pas en Rune : 8 dans l'exemple, la position du premier des deux
   `char` de l'emoji. C'est cohérent avec `Substring` et les plages `[..]`.
2. **`AnyNewLine` change aussi `.`**, qui ne reconnaît plus aucun saut de ligne (sauf avec `Singleline`).
3. **`MoveChunks` vide la source** : elle reste utilisable, mais sa `Length` vaut 0.

## Sources

- [What's new in .NET 11 libraries — Strings, Base64, Unicode, Regex](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
