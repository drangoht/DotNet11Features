# 04 · Indexeurs d'extension (C# 15)

> **En une phrase** : un bloc `extension(JsonElement e) { public JsonElement this[string nom] => … }` ajoute la
> syntaxe `racine["commande"]["client"]` à un type qu'on ne possède pas.
>
> Code : [`E04_IndexeursExtension.cs`](../src/NouveautesDotNet11.Exemples/Exemples/E04_IndexeursExtension.cs) ·
> Lancer : `dotnet run -c Release --project src/NouveautesDotNet11.Exemples -- indexeurs`

---

## 1. L'idée expliquée simplement

Les **méthodes d'extension** (C# 3, 2007) permettent d'ajouter des méthodes à un type qu'on ne peut pas modifier :
`"texte".EnMajuscules()`. C# 14 (.NET 10) a généralisé l'idée avec les **blocs `extension`**, qui acceptent aussi des
propriétés et des membres statiques.

Il manquait les **indexeurs**, c'est-à-dire la syntaxe `objet[clé]`. C# 15 les ajoute.

L'exemple type : `JsonElement` (System.Text.Json, lecture seule et très rapide) n'a **pas** d'indexeur par nom de
propriété. Pour descendre de trois niveaux, il faut enchaîner `GetProperty`.

---

## 2. Comment on faisait avant

```csharp
// Directement
racine.GetProperty("commande").GetProperty("client").GetProperty("ville").GetString();

// Avec une méthode d'extension maison, un peu plus courte
public static JsonElement Prop(this JsonElement element, string nom) => element.GetProperty(nom);
racine.Prop("commande").Prop("client").Prop("ville").GetString();
```

Ça marche, mais on perd la syntaxe naturelle d'un accès par clé, et une méthode ne peut pas servir à
l'**affectation** (`x[clé] = valeur`) quand le type le permettrait.

---

## 3. Ce que change C# 15

```csharp
public static class ExtensionsIndexeurs
{
    extension(JsonElement element)
    {
        public JsonElement this[string nom] => element.GetProperty(nom);
    }

    extension(IReadOnlyList<LigneCommande> lignes)
    {
        public LigneCommande this[string sku] => lignes.First(l => l.Sku == sku);
    }

    extension<T>(IEnumerable<T> source)          // générique, avec Index (^1 = le dernier)
    {
        public T this[Index position] => source.ElementAt(position);
    }
}

string ville = racine["commande"]["client"]["ville"].GetString();
LigneCommande the = lignes["THE-VERT"];
int dernier = nombres[^1];                        // sur une séquence LINQ, sans .ToList()
```

### Côte à côte

<table>
<tr><th>Avant : méthode d'extension</th><th>C# 15 : indexeur d'extension</th></tr>
<tr><td valign="top">

```csharp
public static JsonElement Prop(
  this JsonElement e, string nom)
  => e.GetProperty(nom);

racine.Prop("commande")
      .Prop("client")
      .Prop("ville")
```

</td><td valign="top">

```csharp
extension(JsonElement e)
{
  public JsonElement this[string nom]
    => e.GetProperty(nom);
}

racine["commande"]["client"]["ville"]

```

</td></tr>
</table>

Le code généré est le même dans les deux cas : un appel de méthode statique. C'est une amélioration de
**lisibilité**, sans coût à l'exécution.

---

## 4. Résultats de l'exemple

```
── 04 · Indexeurs d'extension (C# 15) ───────────────────────
   ▸ Après : racine["commande"]["client"]["ville"]
     Lyon
     ✓ l'indexeur d'extension se chaîne comme un indexeur natif
   ▸ Un indexeur métier sur IReadOnlyList<LigneCommande>
     lignes["THE-VERT"] → Thé vert × 1
     ✓ recherche par code article avec la syntaxe [ ]
     ✓ l'indexeur natif lignes[0] reste prioritaire et inchangé
   ▸ Un indexeur générique sur IEnumerable<T>, avec ^1 (depuis la fin)
     nombres = 3, 6, 9 ; nombres[^1] = 9
     ✓ ^1 sur une séquence LINQ sans .ToList()
```

---

## 5. Pièges et limites

1. **Les membres du type gagnent toujours** : `lignes[0]` appelle l'indexeur natif de la liste, pas l'extension.
   Une extension ne peut qu'ajouter des surcharges que le type n'a pas (ici, un indexeur par `string`).
2. **Le paramètre du bloc doit être nommé** (`extension(JsonElement element)`, pas `extension(JsonElement)`) : un
   indexeur est toujours un membre d'instance, il lui faut un récepteur.
3. **Attention à la complexité cachée** : `source[^1]` sur un `IEnumerable<T>` parcourt toute la séquence, et
   `lignes["THE-VERT"]` fait une recherche linéaire. La syntaxe `[ ]` suggère un accès direct : réservez-la aux
   cas où le coût reste raisonnable, ou documentez-le.

## Sources

- [What's new in C# 15 — Extension indexers](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
