# 02 · Hiérarchies fermées (C# 15)

🇬🇧 [English version](../en/02-closed-hierarchies.md)

> **En une phrase** : `public closed record OrderState;` interdit toute classe dérivée en dehors de l'assembly, ce
> qui permet au compilateur de savoir qu'un `switch` sur les trois états connus est **complet**.
>
> Code : [`E02_ClosedHierarchies.cs`](../../src/DotNet11Features.Examples/Examples/E02_ClosedHierarchies.cs) ·
> Lancer : `dotnet run -c Release --project src/DotNet11Features.Examples -- closed`

---

## 1. L'idée expliquée simplement

### L'image du feu tricolore

Un feu a trois couleurs. Si un fabricant vous livrait un jour un feu « violet », tout le code de circulation serait
faux. Le code qui gère un feu **compte sur le fait que la liste est fermée**.

En C#, une classe `abstract` est au contraire **ouverte** : n'importe quel autre projet peut en dériver. Le
compilateur ne peut donc jamais affirmer qu'un `switch` sur ses dérivées est complet, et il exige un bras
« par défaut » qui, en pratique, ne sert qu'à lever une exception.

`closed` ferme la liste : **seul l'assembly qui déclare la classe** peut en dériver directement.

### Union ou hiérarchie fermée ?

| | [Union](01-unions.md) | Hiérarchie fermée |
|---|---|---|
| Les cas sont… | des types existants, sans lien | des classes qui dérivent d'une base commune |
| On possède les types ? | pas forcément (`int`, `Guid`…) | oui, dans le même assembly |
| Membres communs | non | oui (propriétés et méthodes de la base) |
| Représentation | struct qui enveloppe un `object` | classes normales, aucune enveloppe |
| Exemple typique | `Card \| BankTransfer \| Voucher` | `Draft`, `Paid`, `Shipped` d'une commande |

---

## 2. Comment on faisait avant

```csharp
public abstract record OrderStateBefore;
public record DraftBefore : OrderStateBefore;
public record PaidBefore(DateOnly On) : OrderStateBefore;
public record ShippedBefore(string TrackingNumber) : OrderStateBefore;

static string LabelBefore(OrderStateBefore state) => state switch
{
    DraftBefore => "draft",
    PaidBefore p => $"paid on {p.On:MM/dd}",
    ShippedBefore s => $"shipped ({s.TrackingNumber})",
    _ => throw new UnreachableException($"Unknown state: {state.GetType().Name}"),   // obligatoire
};
```

Le dernier bras est le symptôme. Le jour où quelqu'un ajoute `DeliveredBefore`, **rien ne casse à la compilation** :
le premier client dont la commande est livrée déclenche `UnreachableException` en production.

---

## 3. Ce que change C# 15

```csharp
public closed record OrderState;                    // ← un seul mot ajouté
public record Draft : OrderState;
public record Paid(DateOnly On) : OrderState;
public record Shipped(string TrackingNumber) : OrderState;

static string Label(OrderState state) => state switch
{
    Draft => "draft",
    Paid p => $"paid on {p.On:MM/dd}",
    Shipped s => $"shipped ({s.TrackingNumber})",
};                                                  // ← complet : aucun bras par défaut
```

Ajouter `public record Delivered : OrderState;` fait maintenant échouer **chaque** `switch` qui l'oublie :

```
error CS8509: L'expression switch ne prend pas en charge toutes les valeurs possibles de son type d'entrée
(elle n'est pas exhaustive). Par exemple, le modèle 'Delivered' n'est pas couvert.
```

(CS8509 est un avertissement par défaut ; ce dépôt le passe en erreur dans `Directory.Build.props`.)

### Ce que le compilateur génère vraiment

```
[IsClosedType(DerivedTypes = Draft, Paid, Shipped)]
✓ a closed class is implicitly abstract (new OrderState() is not allowed)
✓ the list of direct derived types is written into the assembly
```

La liste des dérivées est **écrite dans les métadonnées** : un autre assembly qui consomme `OrderState` connaît
lui aussi la liste complète, et ses propres `switch` sont vérifiés.

```mermaid
flowchart TB
    E["closed record OrderState<br/>[IsClosedType(Draft, Paid, Shipped)]"]
    E --> B[Draft]
    E --> P["Paid(On)"]
    E --> X["Shipped(TrackingNumber)"]
    A["Autre assembly :<br/>record Cancelled : OrderState"] -. "❌ interdit" .-> E
```

### Côte à côte

<table>
<tr><th>Avant : abstract</th><th>C# 15 : closed</th></tr>
<tr><td valign="top">

```csharp
public abstract record State;

state switch
{
  Draft => …,
  Paid p => …,
  Shipped s => …,
  _ => throw new
    UnreachableException(),
};
```

</td><td valign="top">

```csharp
public closed record State;

state switch
{
  Draft => …,
  Paid p => …,
  Shipped s => …,
};


```

</td></tr>
<tr><td>

Nouvel état oublié : **exception en production**.

</td><td>

Nouvel état oublié : **erreur de compilation**, à chaque endroit concerné.

</td></tr>
</table>

---

## 4. Sortie de l'exemple

```
── 02 · Closed hierarchies (C# 15) ──────────────────────────
   ▸ After: public closed record OrderState;
     draft                    cancellable: yes
     paid on 11/10            cancellable: yes
     shipped (PARCEL-42)      cancellable: no
```

Aucun coût à l'exécution : ce sont des classes ordinaires, le `switch` produit exactement le même code machine
qu'avant (une série de tests de type). Seul le bras `_ => throw` disparaît.

---

## 5. Pièges et limites

1. **`closed` implique `abstract`** : on ne peut ni l'instancier, ni le combiner avec `sealed`, `static` ou un
   `abstract` explicite.
2. **Seules les dérivées *directes* sont contrôlées.** Si `Paid` a elle-même des dérivées (`PaidByCard`,
   `PaidByTransfer`), marquez `Paid` aussi `closed` pour que le compilateur connaisse toute l'arborescence.
3. **La fermeture vaut pour l'assembly**, pas pour le fichier : un collègue peut ajouter une dérivée ailleurs dans le
   même projet. C'est voulu, et c'est justement CS8509 qui lui montrera tous les `switch` à compléter.
4. **`null` reste possible** pour une variable de type référence nullable : `OrderState? s` demande un bras `null`.
5. **JSON** : System.Text.Json peut déduire le polymorphisme d'une hiérarchie fermée sans liste de
   `[JsonDerivedType]` (voir [l'exemple 10](10-json-unions.md)).

## Sources

- [What's new in C# 15 — Closed hierarchies](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
