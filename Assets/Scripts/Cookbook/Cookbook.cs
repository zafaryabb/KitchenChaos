using System.Collections.Generic;
using System.Text;

/// <summary>
/// Works out the step-by-step preparation procedure for a dish by tracing the
/// recipe graph backwards (final plated item → cook step → cut steps → source).
/// Pure logic with no scene dependencies, so it powers the order logger now and a
/// cookbook UI later.
/// </summary>
public static class Cookbook
{
    public enum ActionType { Take, Cut, Cook, Plate }

    public class Step
    {
        public ActionType action;
        public KitchenObjectSO from;      // input item (null for Take/Plate)
        public KitchenObjectSO result;    // produced item
        public CuttingRecipeSO.ProcessVerb verb;
        public int cutCount;
        public float cookTime;
        public KitchenObjectSO byproduct; // scrap left behind (may be null)
        public string text;               // human-readable line
    }

    /// <summary>Ordered steps to make the whole dish, ending with "plate &amp; serve".</summary>
    public static List<Step> GetProcedure(RecipeDatabaseSO db, MenuRecipeSO dish)
    {
        var steps = new List<Step>();
        if (db == null || dish == null || dish.ingredients == null)
        {
            return steps;
        }

        var doneIngredients = new HashSet<KitchenObjectSO>();
        foreach (KitchenObjectSO ingredient in dish.ingredients)
        {
            if (ingredient == null || !doneIngredients.Add(ingredient))
            {
                continue; // skip nulls and repeated ingredients
            }
            BuildFor(db, ingredient, steps, new HashSet<KitchenObjectSO>());
        }

        steps.Add(new Step
        {
            action = ActionType.Plate,
            text = "Plate everything and deliver it at the serving counter",
        });
        return steps;
    }

    private static void BuildFor(RecipeDatabaseSO db, KitchenObjectSO item, List<Step> steps, HashSet<KitchenObjectSO> guard)
    {
        if (item == null || !guard.Add(item))
        {
            return; // guard against accidental cycles in the data
        }

        FryingRecipeSO fry = FindFry(db, item);
        if (fry != null)
        {
            BuildFor(db, fry.from, steps, guard);
            steps.Add(new Step
            {
                action = ActionType.Cook,
                from = fry.from,
                result = item,
                cookTime = fry.fryingTime,
                text = $"Cook {Name(fry.from)} → {Name(item)} on the stove (~{fry.fryingTime:0}s)",
            });
            return;
        }

        CuttingRecipeSO cut = FindCut(db, item);
        if (cut != null)
        {
            BuildFor(db, cut.from, steps, guard);
            string scrap = cut.byproduct != null ? $"  (leaves {Name(cut.byproduct)} for the cat)" : "";
            steps.Add(new Step
            {
                action = ActionType.Cut,
                from = cut.from,
                result = item,
                verb = cut.verb,
                cutCount = cut.cutCount,
                byproduct = cut.byproduct,
                text = $"{Verb(cut.verb)} {Name(cut.from)} → {Name(item)} at the board (×{cut.cutCount}){scrap}",
            });
            return;
        }

        // no recipe produces it → it's a raw source item from a container
        steps.Add(new Step
        {
            action = ActionType.Take,
            result = item,
            text = $"Take {Name(item)} from the container",
        });
    }

    private static FryingRecipeSO FindFry(RecipeDatabaseSO db, KitchenObjectSO to)
    {
        foreach (FryingRecipeSO r in db.fryingRecipes)
        {
            if (r != null && r.to == to) return r;
        }
        return null;
    }

    private static CuttingRecipeSO FindCut(RecipeDatabaseSO db, KitchenObjectSO to)
    {
        foreach (CuttingRecipeSO r in db.cuttingRecipes)
        {
            if (r != null && r.to == to) return r;
        }
        return null;
    }

    private static string Name(KitchenObjectSO so)
    {
        if (so == null) return "?";
        return string.IsNullOrEmpty(so.objectName) ? so.name : so.objectName;
    }

    private static string Verb(CuttingRecipeSO.ProcessVerb v) => v switch
    {
        CuttingRecipeSO.ProcessVerb.Fillet => "Fillet",
        CuttingRecipeSO.ProcessVerb.Slice => "Slice",
        CuttingRecipeSO.ProcessVerb.Peel => "Peel",
        CuttingRecipeSO.ProcessVerb.Shuck => "Shuck",
        CuttingRecipeSO.ProcessVerb.Ring => "Ring",
        CuttingRecipeSO.ProcessVerb.Chop => "Chop",
        _ => "Cut",
    };

    /// <summary>A tidy multi-line string of the procedure, for the Console / a UI.</summary>
    public static string Format(MenuRecipeSO dish, List<Step> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📖 <b>{(dish != null ? dish.recipeName : "?")}</b>");
        for (int i = 0; i < steps.Count; i++)
        {
            sb.AppendLine($"   {i + 1}. {steps[i].text}");
        }
        return sb.ToString();
    }
}
