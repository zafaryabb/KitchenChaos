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
///   • a "cooked" tinted material (the pack ships one shared material, so cooked
///     seafood is represented by a warm tint),
///   • KitchenObjectSO assets for every prep stage AND cooked variant (+ matching
///     item prefabs that wrap the seafood pack meshes),
///   • CuttingRecipeSO chains (Whole → Half → Fillet → Sashimi, shuck, peel, ring…),
///   • FryingRecipeSO recipes for the stove (Fillet → Grilled, Rings → Calamari…),
///   • MenuRecipeSO dishes (raw bar + cooked) with plated "presentation" prefabs,
///   • a MenuSO (_FishMenu) listing every dish.
///
/// Design note: the fillet/meat stage is the BRANCH POINT — cut it further for a
/// raw dish (sashimi) or take it to the stove for a cooked dish. So prep AND
/// cooking are both core. Future stations (dressing/seasoning) slot in the same
/// way: add a ProcessVerb + a counter that swaps item A → item B via a recipe SO.
///
/// Idempotent — re-run any time after editing the tables below.
/// </summary>
public static class FishContentGenerator
{
    private const string Root = "Assets/_FishGame";
    private const string SoDir = Root + "/KitchenObjectSO";
    private const string PrefabDir = Root + "/Prefabs";
    private const string CutDir = Root + "/CuttingRecipeSO";
    private const string FryDir = Root + "/FryingRecipeSO";
    private const string MenuDir = Root + "/MenuRecipeSO";
    private const string PlatedDir = Root + "/PlatedVisuals";
    private const string MatDir = Root + "/Materials";
    private const string MenuAsset = Root + "/_FishMenu.asset";
    private const string RecipeDbAsset = Root + "/_RecipeDatabase.asset";
    private const string CookedMatPath = MatDir + "/Seafood_Cooked.mat";

    private const string SeafoodDir = "Assets/Mnostva_Art/Cartoon_Seafood_Pack/Prefab/Seafood";
    private const string SourceMatPath = "Assets/Mnostva_Art/Cartoon_Seafood_Pack/Material/Color_Mat.mat";

    private static readonly Color CookedTint = new Color(0.95f, 0.68f, 0.45f); // warm grilled multiply

    private enum MatKind { Raw, Cooked }

    private static Material cookedMaterial;

    [MenuItem("Tide & Table/Generate Fish Content")]
    public static void Generate()
    {
        EnsureFolder(SoDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(CutDir);
        EnsureFolder(FryDir);
        EnsureFolder(MenuDir);
        EnsureFolder(PlatedDir);
        EnsureFolder(MatDir);

        cookedMaterial = CreateOrLoadCookedMaterial();

        var idToSO = new Dictionary<string, KitchenObjectSO>();
        int items = 0, cuts = 0, fries = 0, menus = 0, missing = 0;

        try
        {
            for (int i = 0; i < Items.Length; i++)
            {
                ItemDef def = Items[i];
                EditorUtility.DisplayProgressBar("Generating fish content", "Item: " + def.id, (float)i / Items.Length);
                idToSO[def.id] = CreateOrLoadItem(def, ref missing);
                items++;
            }

            var allCuts = new List<CuttingRecipeSO>();
            foreach (CutDef def in Cuts)
            {
                CuttingRecipeSO r = CreateOrLoadCut(def, idToSO);
                if (r != null) { allCuts.Add(r); cuts++; }
            }

            var allFries = new List<FryingRecipeSO>();
            foreach (FryDef def in Fries)
            {
                FryingRecipeSO r = CreateOrLoadFry(def, idToSO);
                if (r != null) { allFries.Add(r); fries++; }
            }

            var menuRecipes = new List<MenuRecipeSO>();
            foreach (MenuDef def in Menus)
            {
                EditorUtility.DisplayProgressBar("Generating fish content", "Dish: " + def.recipeName, 0.7f);
                menuRecipes.Add(CreateOrLoadMenu(def, idToSO, ref missing));
                menus++;
            }

            MenuSO menu = LoadOrCreate<MenuSO>(MenuAsset);
            menu.menuRecipes = menuRecipes;
            EditorUtility.SetDirty(menu);

            // recipe graph for the cookbook / order procedure logger
            RecipeDatabaseSO db = LoadOrCreate<RecipeDatabaseSO>(RecipeDbAsset);
            db.cuttingRecipes = allCuts;
            db.fryingRecipes = allFries;
            db.dishes = menuRecipes;
            EditorUtility.SetDirty(db);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=cyan>[Tide & Table]</color> Generated {items} items, {cuts} cutting recipes, " +
                  $"{fries} frying recipes, {menus} dishes. Missing source meshes: {missing}.\n" +
                  $"• Assign <b>{MenuAsset}</b> to DeliveryManager + MenuManager.\n" +
                  $"• Drag <b>{CutDir}</b> onto each cutting station's 'Cutting Recipes'.\n" +
                  $"• Drag <b>{FryDir}</b> onto each stove's 'Frying Recipes'.\n" +
                  $"• Run <b>Tide & Table ▸ Print Cookbook</b> to see every dish's steps, or assign " +
                  $"<b>{RecipeDbAsset}</b> to an OrderProcedureLogger for per-order logging.");
    }

    #region Material

    private static Material CreateOrLoadCookedMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(CookedMatPath);
        if (mat == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMatPath);
            if (source == null)
            {
                Debug.LogWarning($"[Tide & Table] Source seafood material not found at {SourceMatPath}; cooked items will not be tinted.");
                return null;
            }
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, CookedMatPath);
        }

        // tint the base colour warm so the same mesh reads as "grilled / cooked"
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", CookedTint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", CookedTint);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion

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
            if (def.mat == MatKind.Cooked && cookedMaterial != null)
            {
                ApplyMaterial(visual, cookedMaterial);
            }
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void ApplyMaterial(GameObject go, Material mat)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    #endregion

    #region Cutting & frying recipe generation

    private static CuttingRecipeSO CreateOrLoadCut(CutDef def, Dictionary<string, KitchenObjectSO> dict)
    {
        if (!dict.TryGetValue(def.from, out KitchenObjectSO from) ||
            !dict.TryGetValue(def.to, out KitchenObjectSO to))
        {
            Debug.LogWarning($"[Tide & Table] Cut recipe references unknown item: {def.from} -> {def.to}");
            return null;
        }

        string path = $"{CutDir}/{def.from}__to__{def.to}.asset";
        CuttingRecipeSO recipe = LoadOrCreate<CuttingRecipeSO>(path);
        recipe.from = from;
        recipe.to = to;
        recipe.cutCount = def.cutCount;
        recipe.verb = def.verb;
        recipe.byproduct = (def.byproduct != null && dict.TryGetValue(def.byproduct, out KitchenObjectSO bp)) ? bp : null;
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static FryingRecipeSO CreateOrLoadFry(FryDef def, Dictionary<string, KitchenObjectSO> dict)
    {
        if (!dict.TryGetValue(def.from, out KitchenObjectSO from) ||
            !dict.TryGetValue(def.to, out KitchenObjectSO to))
        {
            Debug.LogWarning($"[Tide & Table] Fry recipe references unknown item: {def.from} -> {def.to}");
            return null;
        }

        string path = $"{FryDir}/{def.from}__fry__{def.to}.asset";
        FryingRecipeSO recipe = LoadOrCreate<FryingRecipeSO>(path);
        recipe.from = from;
        recipe.to = to;
        recipe.fryingTime = def.time;
        recipe.shouldWarning = false; // calm cooking: no burn alarms
        EditorUtility.SetDirty(recipe);
        return recipe;
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
            if (dict.TryGetValue(ing.ingredientId, out KitchenObjectSO so)) ingredients.Add(so);
            else Debug.LogWarning($"[Tide & Table] Dish '{def.recipeName}' references unknown item {ing.ingredientId}");
        }
        recipe.ingredients = ingredients;
        recipe.prefabVisual = BuildPlatedVisual(def, ref missing);
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

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
                if (src == null) { missing++; continue; }
                var m = (GameObject)PrefabUtility.InstantiatePrefab(src);
                m.transform.SetParent(group.transform, false);
                m.transform.localPosition = pm.pos;
                m.transform.localEulerAngles = pm.euler;
                m.transform.localScale = Vector3.one * pm.scale;
                if (pm.mat == MatKind.Cooked && cookedMaterial != null) ApplyMaterial(m, cookedMaterial);
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
        if (go == null) Debug.LogWarning($"[Tide & Table] Missing seafood prefab: {path}");
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
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static string Sanitize(string s) => s.Replace(" ", "_");

    #endregion

    #region Content tables  (edit these to add fish / dishes, then re-run)

    private class ItemDef
    {
        public string id, displayName, visualPrefab;
        public bool petFood;
        public float visualScale;
        public MatKind mat;

        public ItemDef(string id, string displayName, string visualPrefab,
            bool petFood = false, float visualScale = 1f, MatKind mat = MatKind.Raw)
        {
            this.id = id; this.displayName = displayName; this.visualPrefab = visualPrefab;
            this.petFood = petFood; this.visualScale = visualScale; this.mat = mat;
        }
    }

    private class CutDef
    {
        public string from, to, byproduct;
        public int cutCount;
        public CuttingRecipeSO.ProcessVerb verb;

        public CutDef(string from, string to, int cutCount, CuttingRecipeSO.ProcessVerb verb, string byproduct = null)
        {
            this.from = from; this.to = to; this.cutCount = cutCount; this.verb = verb; this.byproduct = byproduct;
        }
    }

    private class FryDef
    {
        public string from, to;
        public float time;
        public FryDef(string from, string to, float time) { this.from = from; this.to = to; this.time = time; }
    }

    private class PlacedMesh
    {
        public string prefab;
        public Vector3 pos, euler;
        public float scale;
        public MatKind mat;
        public PlacedMesh(string prefab, Vector3 pos, Vector3 euler, float scale, MatKind mat)
        { this.prefab = prefab; this.pos = pos; this.euler = euler; this.scale = scale; this.mat = mat; }
    }

    private class PlatedIngredient
    {
        public string ingredientId;
        public PlacedMesh[] meshes;
        public PlatedIngredient(string ingredientId, params PlacedMesh[] meshes)
        { this.ingredientId = ingredientId; this.meshes = meshes; }
    }

    private class MenuDef
    {
        public string recipeName;
        public PlatedIngredient[] plated;
        public MenuDef(string recipeName, params PlatedIngredient[] plated) { this.recipeName = recipeName; this.plated = plated; }
    }

    // readable table helpers
    private static PlacedMesh Mesh(string prefab, float x, float z, float ry, float scale = 1f)
        => new PlacedMesh(prefab, new Vector3(x, 0f, z), new Vector3(0f, ry, 0f), scale, MatKind.Raw);
    private static PlacedMesh CMesh(string prefab, float x, float z, float ry, float scale = 1f)
        => new PlacedMesh(prefab, new Vector3(x, 0f, z), new Vector3(0f, ry, 0f), scale, MatKind.Cooked);
    private static PlatedIngredient PI(string id, params PlacedMesh[] m) => new PlatedIngredient(id, m);

    private static readonly ItemDef[] Items =
    {
        // ── Salmon ──
        new ItemDef("Salmon_Whole",        "Salmon",          "Salmon_1"),
        new ItemDef("Salmon_Half",         "Salmon Half",     "Salmon_1_Half_1"),
        new ItemDef("Salmon_Fillet",       "Salmon Fillet",   "Salmon_1_Fillet_1"),
        new ItemDef("Salmon_Sashimi",      "Salmon Sashimi",  "Salmon_1_Slice"),
        new ItemDef("Salmon_FilletCooked", "Grilled Salmon",  "Salmon_1_Fillet_1", mat: MatKind.Cooked),
        // ── Tuna ──
        new ItemDef("Tuna_Whole",          "Tuna",            "Tuna_1"),
        new ItemDef("Tuna_Half",           "Tuna Half",       "Tuna_1_Half_1"),
        new ItemDef("Tuna_Fillet",         "Tuna Fillet",     "Tuna_1_Fillet_1"),
        new ItemDef("Tuna_Sashimi",        "Tuna Sashimi",    "Tuna_1_Slice"),
        new ItemDef("Tuna_FilletCooked",   "Seared Tuna",     "Tuna_1_Fillet_1", mat: MatKind.Cooked),
        // ── Sea Bass ──
        new ItemDef("SeaBass_Whole",        "Sea Bass",         "SeaBass_1"),
        new ItemDef("SeaBass_Half",         "Sea Bass Half",    "SeaBass_1_Half_1"),
        new ItemDef("SeaBass_Fillet",       "Sea Bass Fillet",  "SeaBass_1_Fillet_1"),
        new ItemDef("SeaBass_Sashimi",      "Sea Bass Sashimi", "SeaBass_1_Slice"),
        new ItemDef("SeaBass_FilletCooked", "Grilled Sea Bass", "SeaBass_1_Fillet_1", mat: MatKind.Cooked),
        // ── Sardine (grill only) ──
        new ItemDef("Sardine_Whole",        "Sardine",          "Sardine_1"),
        new ItemDef("Sardine_Half",         "Sardine Half",     "Sardine_1_Half_1"),
        new ItemDef("Sardine_Fillet",       "Sardine Fillet",   "Sardine_1_Fillet_1"),
        new ItemDef("Sardine_FilletCooked", "Grilled Sardine",  "Sardine_1_Fillet_1", mat: MatKind.Cooked),
        // ── Anchovy (cured topping) ──
        new ItemDef("Anchovy_Whole",  "Anchovy",         "Anchovy_1"),
        new ItemDef("Anchovy_Half",   "Anchovy Half",    "Anchovy_1_Half_1"),
        new ItemDef("Anchovy_Fillet", "Anchovy Fillet",  "Anchovy_1_Fillet_1"),
        // ── Shrimp ──
        new ItemDef("Shrimp_Whole",        "Shrimp",         "Shrimp_1"),
        new ItemDef("Shrimp_Peeled",       "Peeled Shrimp",  "Shrimp_1_Peeled_1"),
        new ItemDef("Shrimp_PeeledCooked", "Grilled Shrimp", "Shrimp_1_Peeled_1", mat: MatKind.Cooked),
        // ── Lobster 1 (tail) ──
        new ItemDef("Lobster_Whole",     "Lobster",       "Lobster_1"),
        new ItemDef("Lobster_Split",     "Lobster Split", "Lobster_1_Half_1"),
        new ItemDef("Lobster_Tail",      "Lobster Tail",  "Lobster_1_Peeled_1"),
        new ItemDef("Lobster_TailCooked","Lobster Tail",  "Lobster_1_Peeled_1", mat: MatKind.Cooked),
        // ── Lobster 2 (claws) ──
        new ItemDef("Lobster2_Whole",     "Blue Lobster",   "Lobster_2"),
        new ItemDef("Lobster2_Split",     "Lobster Body",   "Lobster_2_Half_1"),
        new ItemDef("Lobster2_Claw",      "Lobster Claw",   "Lobster_2_Claw_1"),
        new ItemDef("Lobster2_ClawCooked","Lobster Claw",   "Lobster_2_Claw_1", mat: MatKind.Cooked),
        // ── Crab ──
        new ItemDef("Crab_Whole",     "Crab",         "Crab_1"),
        new ItemDef("Crab_Meat",      "Crab Meat",    "Crab_1_Claw_1"),
        new ItemDef("Crab_MeatCooked","Steamed Crab", "Crab_1_Claw_1", mat: MatKind.Cooked),
        // ── Mussel ──
        new ItemDef("Mussel_Closed",    "Mussel",          "Mussel_Closed"),
        new ItemDef("Mussel_Open",      "Mussel (open)",   "Mussel_Open_1"),
        new ItemDef("Mussel_Meat",      "Mussel Meat",     "Mussel_Meat"),
        new ItemDef("Mussel_MeatCooked","Steamed Mussel",  "Mussel_Meat", mat: MatKind.Cooked),
        // ── Oyster (raw) ──
        new ItemDef("Oyster_Closed", "Oyster",        "Oysters_Closed"),
        new ItemDef("Oyster_Open",   "Oyster (open)", "Oysters_Open_1"),
        new ItemDef("Oyster_Meat",   "Fresh Oyster",  "Oysters_Meat"),
        // ── Scallop ──
        new ItemDef("Scallop_Closed",    "Scallop",         "Scallops_Closed"),
        new ItemDef("Scallop_Open",      "Scallop (open)",  "Scallops_Open_1"),
        new ItemDef("Scallop_Meat",      "Scallop",         "Scallops_Meat"),
        new ItemDef("Scallop_MeatCooked","Seared Scallop",  "Scallops_Meat", mat: MatKind.Cooked),
        // ── Squid (calamari) ──
        new ItemDef("Squid_Whole",      "Squid",          "Squid_1"),
        new ItemDef("Squid_Tube",       "Squid Tube",     "Squid_1_Fillet_1"),
        new ItemDef("Squid_Rings",      "Calamari Rings", "Squid_1_Ring_1"),
        new ItemDef("Squid_RingsCooked","Fried Calamari", "Squid_1_Ring_1", mat: MatKind.Cooked),
        // ── Octopus ──
        new ItemDef("Octopus_Whole",      "Octopus",          "Octopus_Tentacles_1"),
        new ItemDef("Octopus_Piece",      "Octopus Piece",    "Octopus_Tentacles_1_Piece_1"),
        new ItemDef("Octopus_Slice",      "Octopus Slice",    "Octopus_Tentacles_1_Slice_1"),
        new ItemDef("Octopus_SliceCooked","Grilled Octopus",  "Octopus_Tentacles_1_Slice_1", mat: MatKind.Cooked),
        // ── Scraps for the cats ──
        new ItemDef("Fish_Skeleton", "Fish Skeleton", "Skeleton_Fish_1",  petFood: true),
        new ItemDef("Crab_Shell",    "Crab Shell",    "Crab_1_Shell",     petFood: true),
        new ItemDef("Mussel_Shell",  "Mussel Shell",  "Mussel_Empty_1",   petFood: true),
        new ItemDef("Oyster_Shell",  "Oyster Shell",  "Oysters_Empty_1",  petFood: true),
        new ItemDef("Scallop_Shell", "Scallop Shell", "Scallops_Empty_1", petFood: true),
    };

    private static readonly CutDef[] Cuts =
    {
        // Fish: Whole → Half → Fillet (→ Sashimi). Fillet is the branch point (cut→sashimi or stove→grilled).
        new CutDef("Salmon_Whole",  "Salmon_Half",   3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Salmon_Half",   "Salmon_Fillet", 3, CuttingRecipeSO.ProcessVerb.Fillet, "Fish_Skeleton"),
        new CutDef("Salmon_Fillet", "Salmon_Sashimi",4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("Tuna_Whole",  "Tuna_Half",   3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Tuna_Half",   "Tuna_Fillet", 3, CuttingRecipeSO.ProcessVerb.Fillet, "Fish_Skeleton"),
        new CutDef("Tuna_Fillet", "Tuna_Sashimi",4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("SeaBass_Whole",  "SeaBass_Half",   3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("SeaBass_Half",   "SeaBass_Fillet", 3, CuttingRecipeSO.ProcessVerb.Fillet, "Fish_Skeleton"),
        new CutDef("SeaBass_Fillet", "SeaBass_Sashimi",4, CuttingRecipeSO.ProcessVerb.Slice),

        new CutDef("Sardine_Whole", "Sardine_Half",   2, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Sardine_Half",  "Sardine_Fillet", 2, CuttingRecipeSO.ProcessVerb.Fillet, "Fish_Skeleton"),

        new CutDef("Anchovy_Whole", "Anchovy_Half",   2, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Anchovy_Half",  "Anchovy_Fillet", 2, CuttingRecipeSO.ProcessVerb.Fillet, "Fish_Skeleton"),

        // Shellfish: peel / split
        new CutDef("Shrimp_Whole",   "Shrimp_Peeled", 3, CuttingRecipeSO.ProcessVerb.Peel),
        new CutDef("Lobster_Whole",  "Lobster_Split", 3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Lobster_Split",  "Lobster_Tail",  3, CuttingRecipeSO.ProcessVerb.Peel),
        new CutDef("Lobster2_Whole", "Lobster2_Split",3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Lobster2_Split", "Lobster2_Claw", 3, CuttingRecipeSO.ProcessVerb.Peel),
        new CutDef("Crab_Whole",     "Crab_Meat",     4, CuttingRecipeSO.ProcessVerb.Shuck, "Crab_Shell"),

        // Mollusks: shuck open → extract meat (+ empty shell scrap)
        new CutDef("Mussel_Closed",  "Mussel_Open",  2, CuttingRecipeSO.ProcessVerb.Shuck),
        new CutDef("Mussel_Open",    "Mussel_Meat",  2, CuttingRecipeSO.ProcessVerb.Shuck, "Mussel_Shell"),
        new CutDef("Oyster_Closed",  "Oyster_Open",  2, CuttingRecipeSO.ProcessVerb.Shuck),
        new CutDef("Oyster_Open",    "Oyster_Meat",  2, CuttingRecipeSO.ProcessVerb.Shuck, "Oyster_Shell"),
        new CutDef("Scallop_Closed", "Scallop_Open", 2, CuttingRecipeSO.ProcessVerb.Shuck),
        new CutDef("Scallop_Open",   "Scallop_Meat", 2, CuttingRecipeSO.ProcessVerb.Shuck, "Scallop_Shell"),

        // Cephalopods
        new CutDef("Squid_Whole",   "Squid_Tube",    3, CuttingRecipeSO.ProcessVerb.Cut),
        new CutDef("Squid_Tube",    "Squid_Rings",   4, CuttingRecipeSO.ProcessVerb.Ring),
        new CutDef("Octopus_Whole", "Octopus_Piece", 3, CuttingRecipeSO.ProcessVerb.Chop),
        new CutDef("Octopus_Piece", "Octopus_Slice", 3, CuttingRecipeSO.ProcessVerb.Slice),
    };

    private static readonly FryDef[] Fries =
    {
        new FryDef("Salmon_Fillet",  "Salmon_FilletCooked",  6f),
        new FryDef("Tuna_Fillet",    "Tuna_FilletCooked",    5f),
        new FryDef("SeaBass_Fillet", "SeaBass_FilletCooked", 6f),
        new FryDef("Sardine_Fillet", "Sardine_FilletCooked", 4f),
        new FryDef("Shrimp_Peeled",  "Shrimp_PeeledCooked",  4f),
        new FryDef("Lobster_Tail",   "Lobster_TailCooked",   7f),
        new FryDef("Lobster2_Claw",  "Lobster2_ClawCooked",  6f),
        new FryDef("Crab_Meat",      "Crab_MeatCooked",      5f),
        new FryDef("Mussel_Meat",    "Mussel_MeatCooked",    4f),
        new FryDef("Scallop_Meat",   "Scallop_MeatCooked",   4f),
        new FryDef("Squid_Rings",    "Squid_RingsCooked",    5f),
        new FryDef("Octopus_Slice",  "Octopus_SliceCooked",  6f),
    };

    // Plated offsets/scales are first-pass guesses — fine-tune the *_Plated prefabs in the editor.
    private static readonly MenuDef[] Menus =
    {
        // ── Raw bar ──
        new MenuDef("Salmon Sashimi",
            PI("Salmon_Sashimi", Mesh("Salmon_1_Slice", -0.06f, 0f, 10f), Mesh("Salmon_1_Slice", 0f, 0f, 0f), Mesh("Salmon_1_Slice", 0.06f, 0f, -10f))),
        new MenuDef("Tuna Sashimi",
            PI("Tuna_Sashimi", Mesh("Tuna_1_Slice", -0.06f, 0f, 10f), Mesh("Tuna_1_Slice", 0f, 0f, 0f), Mesh("Tuna_1_Slice", 0.06f, 0f, -10f))),
        new MenuDef("Sea Bass Sashimi",
            PI("SeaBass_Sashimi", Mesh("SeaBass_1_Slice", -0.06f, 0f, 10f), Mesh("SeaBass_1_Slice", 0f, 0f, 0f), Mesh("SeaBass_1_Slice", 0.06f, 0f, -10f))),
        new MenuDef("Sashimi Trio",
            PI("Salmon_Sashimi", Mesh("Salmon_1_Slice", -0.07f, 0.05f, 10f), Mesh("Salmon_1_Slice", -0.05f, 0.01f, -5f)),
            PI("Tuna_Sashimi",   Mesh("Tuna_1_Slice", 0.07f, 0.05f, 10f), Mesh("Tuna_1_Slice", 0.05f, 0.01f, -5f)),
            PI("SeaBass_Sashimi",Mesh("SeaBass_1_Slice", 0f, -0.06f, 0f), Mesh("SeaBass_1_Slice", 0.02f, -0.03f, 20f))),
        new MenuDef("Fresh Oysters",
            PI("Oyster_Meat", Mesh("Oysters_Meat", -0.06f, 0.04f, 0f), Mesh("Oysters_Meat", 0.05f, 0.03f, 40f), Mesh("Oysters_Meat", 0f, -0.05f, 80f))),
        new MenuDef("Shrimp Cocktail",
            PI("Shrimp_Peeled", Mesh("Shrimp_1_Peeled_1", -0.05f, 0.03f, 20f), Mesh("Shrimp_1_Peeled_1", 0.05f, 0f, -30f), Mesh("Shrimp_1_Peeled_1", 0f, -0.05f, 90f))),
        new MenuDef("Crab Plate",
            PI("Crab_Meat", Mesh("Crab_1_Claw_1", -0.04f, 0f, 20f), Mesh("Crab_1_Claw_1", 0.04f, 0f, -20f))),

        // ── Cooked / grilled ──
        new MenuDef("Grilled Salmon",
            PI("Salmon_FilletCooked", CMesh("Salmon_1_Fillet_1", 0f, 0f, 0f))),
        new MenuDef("Seared Tuna",
            PI("Tuna_FilletCooked", CMesh("Tuna_1_Fillet_1", 0f, 0f, 0f))),
        new MenuDef("Grilled Sea Bass",
            PI("SeaBass_FilletCooked", CMesh("SeaBass_1_Fillet_1", 0f, 0f, 0f))),
        new MenuDef("Grilled Sardines",
            PI("Sardine_FilletCooked", CMesh("Sardine_1_Fillet_1", -0.03f, 0f, 8f), CMesh("Sardine_1_Fillet_1", 0.03f, 0f, -8f))),
        new MenuDef("Fried Calamari",
            PI("Squid_RingsCooked", CMesh("Squid_1_Ring_1", -0.06f, 0.04f, 0f), CMesh("Squid_1_Ring_1", 0.05f, 0.03f, 30f), CMesh("Squid_1_Ring_1", -0.02f, -0.05f, 60f), CMesh("Squid_1_Ring_1", 0.04f, -0.04f, 15f))),
        new MenuDef("Grilled Shrimp",
            PI("Shrimp_PeeledCooked", CMesh("Shrimp_1_Peeled_1", -0.05f, 0.03f, 20f), CMesh("Shrimp_1_Peeled_1", 0.05f, 0f, -30f), CMesh("Shrimp_1_Peeled_1", 0f, -0.05f, 90f))),
        new MenuDef("Steamed Mussels",
            PI("Mussel_MeatCooked", CMesh("Mussel_Meat", -0.05f, 0.03f, 0f), CMesh("Mussel_Meat", 0.05f, 0.03f, 40f), CMesh("Mussel_Meat", -0.03f, -0.05f, 80f), CMesh("Mussel_Meat", 0.04f, -0.04f, 20f))),
        new MenuDef("Seared Scallops",
            PI("Scallop_MeatCooked", CMesh("Scallops_Meat", -0.05f, 0.02f, 0f), CMesh("Scallops_Meat", 0.05f, 0.02f, 30f), CMesh("Scallops_Meat", 0f, -0.05f, 60f))),
        new MenuDef("Grilled Octopus",
            PI("Octopus_SliceCooked", CMesh("Octopus_Tentacles_1_Slice_1", -0.04f, 0f, 10f), CMesh("Octopus_Tentacles_1_Slice_1", 0.04f, 0f, -10f))),
        new MenuDef("Lobster Tail",
            PI("Lobster_TailCooked", CMesh("Lobster_1_Peeled_1", 0f, 0f, 0f))),
        new MenuDef("Lobster Claws",
            PI("Lobster2_ClawCooked", CMesh("Lobster_2_Claw_1", -0.04f, 0f, 15f), CMesh("Lobster_2_Claw_1", 0.04f, 0f, -15f))),
        new MenuDef("Steamed Crab",
            PI("Crab_MeatCooked", CMesh("Crab_1_Claw_1", -0.04f, 0f, 20f), CMesh("Crab_1_Claw_1", 0.04f, 0f, -20f))),

        // ── Combos (raw + cooked together) ──
        new MenuDef("Seafood Platter",
            PI("Salmon_Sashimi",       Mesh("Salmon_1_Slice", -0.07f, 0.05f, 10f)),
            PI("Shrimp_PeeledCooked",  CMesh("Shrimp_1_Peeled_1", 0.07f, 0.05f, 10f)),
            PI("Squid_RingsCooked",    CMesh("Squid_1_Ring_1", -0.06f, -0.05f, 0f)),
            PI("Oyster_Meat",          Mesh("Oysters_Meat", 0.06f, -0.05f, 40f))),
        new MenuDef("Grill Combo",
            PI("Salmon_FilletCooked",  CMesh("Salmon_1_Fillet_1", -0.05f, 0.04f, 10f)),
            PI("Shrimp_PeeledCooked",  CMesh("Shrimp_1_Peeled_1", 0.06f, 0.02f, -20f)),
            PI("Scallop_MeatCooked",   CMesh("Scallops_Meat", 0f, -0.05f, 0f))),
    };

    #endregion
}
