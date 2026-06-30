using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click content generator for the fish restaurant. Run it from the menu:
///   Tide &amp; Table ▸ Generate Fish Content
///
/// It creates, under <c>Assets/_FishGame</c> (leaving the old burger content
/// untouched):
///   • KitchenObjectSO assets for every fish stage (+ matching item prefabs that
///     wrap the seafood pack meshes),
///   • CuttingRecipeSO chains (Whole → Half → Fillet → Sashimi, etc.),
///   • MenuRecipeSO dishes with plated "presentation" prefabs,
///   • a MenuSO (_FishMenu) listing every dish.
///
/// It is idempotent — re-run it any time after editing the tables below and it
/// updates in place. Missing source meshes are logged and skipped, never fatal.
/// </summary>
public static class FishContentGenerator
{
    private const string Root = "Assets/_FishGame";
    private const string SoDir = Root + "/KitchenObjectSO";
    private const string PrefabDir = Root + "/Prefabs";
    private const string CutDir = Root + "/CuttingRecipeSO";
    private const string MenuDir = Root + "/MenuRecipeSO";
    private const string PlatedDir = Root + "/PlatedVisuals";
    private const string MenuAsset = Root + "/_FishMenu.asset";

    private const string SeafoodDir = "Assets/Mnostva_Art/Cartoon_Seafood_Pack/Prefab/Seafood";

    [MenuItem("Tide & Table/Generate Fish Content")]
    public static void Generate()
    {
        EnsureFolder(SoDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(CutDir);
        EnsureFolder(MenuDir);
        EnsureFolder(PlatedDir);

        var idToSO = new Dictionary<string, KitchenObjectSO>();
        int items = 0, cuts = 0, menus = 0, missing = 0;

        try
        {
            // 1. Items (SO + wrapper prefab)
            foreach (ItemDef def in Items)
            {
                KitchenObjectSO so = CreateOrLoadItem(def, ref missing);
                idToSO[def.id] = so;
                items++;
            }

            // 2. Cutting recipe chains
            foreach (CutDef def in Cuts)
            {
                if (CreateOrLoadCut(def, idToSO))
                {
                    cuts++;
                }
            }

            // 3. Menu recipes + plated visuals
            var menuRecipes = new List<MenuRecipeSO>();
            foreach (MenuDef def in Menus)
            {
                MenuRecipeSO recipe = CreateOrLoadMenu(def, idToSO, ref missing);
                menuRecipes.Add(recipe);
                menus++;
            }

            // 4. Menu SO
            MenuSO menu = LoadOrCreate<MenuSO>(MenuAsset);
            menu.menuRecipes = menuRecipes;
            EditorUtility.SetDirty(menu);
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=cyan>[Tide & Table]</color> Generated {items} items, {cuts} cutting recipes, " +
                  $"{menus} dishes. Missing source meshes: {missing}. " +
                  $"Assign <b>{MenuAsset}</b> to DeliveryManager + MenuManager, and drag the " +
                  $"contents of <b>{CutDir}</b> onto your cutting station's 'Cutting Recipes' list.");
    }

    #region Item generation

    private static KitchenObjectSO CreateOrLoadItem(ItemDef def, ref int missing)
    {
        string soPath = $"{SoDir}/{def.id}.asset";
        KitchenObjectSO so = LoadOrCreate<KitchenObjectSO>(soPath);
        so.objectName = def.displayName;
        so.petFood = def.petFood;
        so.prefab = BuildItemPrefab(def, so, ref missing);
        EditorUtility.SetDirty(so);
        return so;
    }

    private static GameObject BuildItemPrefab(ItemDef def, KitchenObjectSO so, ref int missing)
    {
        string prefabPath = $"{PrefabDir}/{def.id}.prefab";

        var root = new GameObject(def.id);
        KitchenObject ko = root.AddComponent<KitchenObject>();

        // assign the private [SerializeField] kitchenObjectSO field
        var serialized = new SerializedObject(ko);
        SerializedProperty soProp = serialized.FindProperty("kitchenObjectSO");
        if (soProp != null)
        {
            soProp.objectReferenceValue = so;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError("[Tide & Table] KitchenObject has no 'kitchenObjectSO' field — did the script change?");
        }

        GameObject visualSrc = LoadSeafood(def.visualPrefab);
        if (visualSrc == null)
        {
            missing++;
        }
        else
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualSrc);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * def.visualScale;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    #endregion

    #region Cutting recipe generation

    private static bool CreateOrLoadCut(CutDef def, Dictionary<string, KitchenObjectSO> dict)
    {
        if (!dict.TryGetValue(def.from, out KitchenObjectSO from) ||
            !dict.TryGetValue(def.to, out KitchenObjectSO to))
        {
            Debug.LogWarning($"[Tide & Table] Cut recipe references unknown item: {def.from} -> {def.to}");
            return false;
        }

        string path = $"{CutDir}/{def.from}__to__{def.to}.asset";
        CuttingRecipeSO recipe = LoadOrCreate<CuttingRecipeSO>(path);
        recipe.from = from;
        recipe.to = to;
        recipe.cutCount = def.cutCount;
        recipe.verb = def.verb;
        recipe.byproduct = (def.byproduct != null && dict.TryGetValue(def.byproduct, out KitchenObjectSO bp)) ? bp : null;
        EditorUtility.SetDirty(recipe);
        return true;
    }

    #endregion

    #region Menu generation

    private static MenuRecipeSO CreateOrLoadMenu(MenuDef def, Dictionary<string, KitchenObjectSO> dict, ref int missing)
    {
        string path = $"{MenuDir}/{Sanitize(def.recipeName)}.asset";
        MenuRecipeSO recipe = LoadOrCreate<MenuRecipeSO>(path);
        recipe.recipeName = def.recipeName;

        var ingredients = new List<KitchenObjectSO>();
        foreach (PlatedIngredient ing in def.plated)
        {
            if (dict.TryGetValue(ing.ingredientId, out KitchenObjectSO so))
            {
                ingredients.Add(so);
            }
            else
            {
                Debug.LogWarning($"[Tide & Table] Dish '{def.recipeName}' references unknown item {ing.ingredientId}");
            }
        }
        recipe.ingredients = ingredients;
        recipe.prefabVisual = BuildPlatedVisual(def, ref missing);
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    /// <summary>
    /// Builds the plated presentation prefab. One child group per ingredient, in
    /// the SAME ORDER as the recipe ingredients — PlateItemVisual disables all
    /// children then re-enables them one by one as ingredients are added.
    /// </summary>
    private static GameObject BuildPlatedVisual(MenuDef def, ref int missing)
    {
        string path = $"{PlatedDir}/{Sanitize(def.recipeName)}_Plated.prefab";
        var root = new GameObject(Sanitize(def.recipeName) + "_Plated");

        foreach (PlatedIngredient ing in def.plated)
        {
            var group = new GameObject(ing.ingredientId);
            group.transform.SetParent(root.transform, false);

            foreach (PlacedMesh pm in ing.meshes)
            {
                GameObject src = LoadSeafood(pm.prefab);
                if (src == null)
                {
                    missing++;
                    continue;
                }
                var m = (GameObject)PrefabUtility.InstantiatePrefab(src);
                m.transform.SetParent(group.transform, false);
                m.transform.localPosition = pm.pos;
                m.transform.localEulerAngles = pm.euler;
                m.transform.localScale = Vector3.one * pm.scale;
            }
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    #endregion

    #region Helpers

    private static GameObject LoadSeafood(string baseName)
    {
        string path = $"{SeafoodDir}/{baseName}.prefab";
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            Debug.LogWarning($"[Tide & Table] Missing seafood prefab: {path}");
        }
        return go;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static string Sanitize(string s)
    {
        return s.Replace(" ", "_");
    }

    #endregion

    #region Content tables  (edit these to add fish / dishes, then re-run)

    private class ItemDef
    {
        public string id;
        public string displayName;
        public string visualPrefab;
        public bool petFood;
        public float visualScale = 1f;

        public ItemDef(string id, string displayName, string visualPrefab, bool petFood = false, float visualScale = 1f)
        {
            this.id = id;
            this.displayName = displayName;
            this.visualPrefab = visualPrefab;
            this.petFood = petFood;
            this.visualScale = visualScale;
        }
    }

    private class CutDef
    {
        public string from;
        public string to;
        public int cutCount;
        public CuttingRecipeSO.ProcessVerb verb;
        public string byproduct;

        public CutDef(string from, string to, int cutCount, CuttingRecipeSO.ProcessVerb verb, string byproduct = null)
        {
            this.from = from;
            this.to = to;
            this.cutCount = cutCount;
            this.verb = verb;
            this.byproduct = byproduct;
        }
    }

    private class PlacedMesh
    {
        public string prefab;
        public Vector3 pos;
        public Vector3 euler;
        public float scale;

        public PlacedMesh(string prefab, Vector3 pos, Vector3 euler, float scale = 1f)
        {
            this.prefab = prefab;
            this.pos = pos;
            this.euler = euler;
            this.scale = scale;
        }
    }

    private class PlatedIngredient
    {
        public string ingredientId;
        public PlacedMesh[] meshes;

        public PlatedIngredient(string ingredientId, params PlacedMesh[] meshes)
        {
            this.ingredientId = ingredientId;
            this.meshes = meshes;
        }
    }

    private class MenuDef
    {
        public string recipeName;
        public PlatedIngredient[] plated;

        public MenuDef(string recipeName, params PlatedIngredient[] plated)
        {
            this.recipeName = recipeName;
            this.plated = plated;
        }
    }

    // Small helpers to keep the tables readable.
    private static PlacedMesh Mesh(string prefab, float x, float z, float ry, float scale = 1f)
        => new PlacedMesh(prefab, new Vector3(x, 0f, z), new Vector3(0f, ry, 0f), scale);

    private static readonly ItemDef[] Items =
    {
        // Salmon line
        new ItemDef("Salmon_Whole",   "Salmon",          "Salmon_1"),
        new ItemDef("Salmon_Half",    "Salmon Half",     "Salmon_1_Half_1"),
        new ItemDef("Salmon_Fillet",  "Salmon Fillet",   "Salmon_1_Fillet_1"),
        new ItemDef("Salmon_Sashimi", "Salmon Sashimi",  "Salmon_1_Slice"),
        // Tuna line
        new ItemDef("Tuna_Whole",     "Tuna",            "Tuna_1"),
        new ItemDef("Tuna_Half",      "Tuna Half",       "Tuna_1_Half_1"),
        new ItemDef("Tuna_Fillet",    "Tuna Fillet",     "Tuna_1_Fillet_1"),
        new ItemDef("Tuna_Sashimi",   "Tuna Sashimi",    "Tuna_1_Slice"),
        // Sea bass line
        new ItemDef("SeaBass_Whole",  "Sea Bass",        "SeaBass_1"),
        new ItemDef("SeaBass_Half",   "Sea Bass Half",   "SeaBass_1_Half_1"),
        new ItemDef("SeaBass_Fillet", "Sea Bass Fillet", "SeaBass_1_Fillet_1"),
        new ItemDef("SeaBass_Sashimi","Sea Bass Sashimi","SeaBass_1_Slice"),
        // Squid line
        new ItemDef("Squid_Whole",    "Squid",           "Squid_1"),
        new ItemDef("Squid_Tube",     "Squid Tube",      "Squid_1_Fillet_1"),
        new ItemDef("Squid_Rings",    "Calamari Rings",  "Squid_1_Ring_1"),
        // Shrimp line
        new ItemDef("Shrimp_Whole",   "Shrimp",          "Shrimp_1"),
        new ItemDef("Shrimp_Peeled",  "Peeled Shrimp",   "Shrimp_1_Peeled_1"),
        // Crab line
        new ItemDef("Crab_Whole",     "Crab",            "Crab_1"),
        new ItemDef("Crab_Meat",      "Crab Meat",       "Crab_1_Claw_1"),
        // Scraps for the cats
        new ItemDef("Crab_Shell",     "Crab Shell",      "Crab_1_Shell",   petFood: true),
        new ItemDef("Fish_Skeleton",  "Fish Skeleton",   "Skeleton_Fish_1", petFood: true),
    };

    private static readonly CutDef[] Cuts =
    {
        new CutDef("Salmon_Whole",  "Salmon_Half",    3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Salmon_Half",   "Salmon_Fillet",  3, CuttingRecipeSO.ProcessVerb.Fillet, byproduct: "Fish_Skeleton"),
        new CutDef("Salmon_Fillet", "Salmon_Sashimi", 4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("Tuna_Whole",    "Tuna_Half",      3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Tuna_Half",     "Tuna_Fillet",    3, CuttingRecipeSO.ProcessVerb.Fillet, byproduct: "Fish_Skeleton"),
        new CutDef("Tuna_Fillet",   "Tuna_Sashimi",   4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("SeaBass_Whole", "SeaBass_Half",   3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("SeaBass_Half",  "SeaBass_Fillet", 3, CuttingRecipeSO.ProcessVerb.Fillet, byproduct: "Fish_Skeleton"),
        new CutDef("SeaBass_Fillet","SeaBass_Sashimi",4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("Squid_Whole",   "Squid_Tube",     3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Squid_Tube",    "Squid_Rings",    4, CuttingRecipeSO.ProcessVerb.Ring),

        new CutDef("Shrimp_Whole",  "Shrimp_Peeled",  3, CuttingRecipeSO.ProcessVerb.Peel),

        new CutDef("Crab_Whole",    "Crab_Meat",      4, CuttingRecipeSO.ProcessVerb.Shuck, byproduct: "Crab_Shell"),
    };

    // NOTE: plated mesh offsets/scales are first-pass guesses — fine-tune the
    // generated *_Plated prefabs in the editor once you see them on a plate.
    private static readonly MenuDef[] Menus =
    {
        new MenuDef("Salmon Sashimi",
            new PlatedIngredient("Salmon_Sashimi",
                Mesh("Salmon_1_Slice", -0.06f, 0f,  10f),
                Mesh("Salmon_1_Slice",  0.00f, 0f,   0f),
                Mesh("Salmon_1_Slice",  0.06f, 0f, -10f))),

        new MenuDef("Tuna Sashimi",
            new PlatedIngredient("Tuna_Sashimi",
                Mesh("Tuna_1_Slice", -0.06f, 0f,  10f),
                Mesh("Tuna_1_Slice",  0.00f, 0f,   0f),
                Mesh("Tuna_1_Slice",  0.06f, 0f, -10f))),

        new MenuDef("Sea Bass Sashimi",
            new PlatedIngredient("SeaBass_Sashimi",
                Mesh("SeaBass_1_Slice", -0.06f, 0f,  10f),
                Mesh("SeaBass_1_Slice",  0.00f, 0f,   0f),
                Mesh("SeaBass_1_Slice",  0.06f, 0f, -10f))),

        new MenuDef("Calamari Rings",
            new PlatedIngredient("Squid_Rings",
                Mesh("Squid_1_Ring_1", -0.06f,  0.04f,  0f),
                Mesh("Squid_1_Ring_1",  0.05f,  0.03f, 30f),
                Mesh("Squid_1_Ring_1", -0.02f, -0.05f, 60f),
                Mesh("Squid_1_Ring_1",  0.04f, -0.04f, 15f))),

        new MenuDef("Shrimp Plate",
            new PlatedIngredient("Shrimp_Peeled",
                Mesh("Shrimp_1_Peeled_1", -0.05f,  0.03f,  20f),
                Mesh("Shrimp_1_Peeled_1",  0.05f,  0.00f, -30f),
                Mesh("Shrimp_1_Peeled_1",  0.00f, -0.05f,  90f))),

        new MenuDef("Crab Plate",
            new PlatedIngredient("Crab_Meat",
                Mesh("Crab_1_Claw_1", -0.04f, 0f,  20f),
                Mesh("Crab_1_Claw_1",  0.04f, 0f, -20f))),

        new MenuDef("Sashimi Trio",
            new PlatedIngredient("Salmon_Sashimi",
                Mesh("Salmon_1_Slice", -0.07f, 0.05f, 10f),
                Mesh("Salmon_1_Slice", -0.05f, 0.01f, -5f)),
            new PlatedIngredient("Tuna_Sashimi",
                Mesh("Tuna_1_Slice", 0.07f, 0.05f, 10f),
                Mesh("Tuna_1_Slice", 0.05f, 0.01f, -5f)),
            new PlatedIngredient("SeaBass_Sashimi",
                Mesh("SeaBass_1_Slice", 0.00f, -0.06f, 0f),
                Mesh("SeaBass_1_Slice", 0.02f, -0.03f, 20f))),

        new MenuDef("Seafood Platter",
            new PlatedIngredient("Salmon_Sashimi",
                Mesh("Salmon_1_Slice", -0.07f, 0.05f, 10f)),
            new PlatedIngredient("Tuna_Sashimi",
                Mesh("Tuna_1_Slice", 0.07f, 0.05f, 10f)),
            new PlatedIngredient("Shrimp_Peeled",
                Mesh("Shrimp_1_Peeled_1", -0.06f, -0.05f, 40f)),
            new PlatedIngredient("Squid_Rings",
                Mesh("Squid_1_Ring_1", 0.06f, -0.05f, 0f))),
    };

    #endregion
}
