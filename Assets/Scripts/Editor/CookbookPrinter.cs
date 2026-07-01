using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dumps the whole cookbook (every dish's step-by-step procedure) to the Console,
/// no Play mode needed. Menu: Tide &amp; Table ▸ Print Cookbook.
/// </summary>
public static class CookbookPrinter
{
    private const string DbPath = "Assets/_FishGame/_RecipeDatabase.asset";

    [MenuItem("Tide & Table/Print Cookbook")]
    public static void Print()
    {
        var db = AssetDatabase.LoadAssetAtPath<RecipeDatabaseSO>(DbPath);
        if (db == null)
        {
            Debug.LogWarning($"[Tide & Table] No recipe database at {DbPath}. Run 'Generate Fish Content' first.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<b>📖 TIDE & TABLE — COOKBOOK ({db.dishes.Count} dishes)</b>\n");
        foreach (MenuRecipeSO dish in db.dishes)
        {
            var steps = Cookbook.GetProcedure(db, dish);
            sb.AppendLine(Cookbook.Format(dish, steps));
        }
        Debug.Log(sb.ToString());
    }
}
