using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One-click restaurant builder for the Restaurant scene. Menu:
///   Tide &amp; Table ▸ Build Restaurant   (and ▸ Clear Restaurant Build)
///
/// v2 — cozy &amp; walkable:
///  • Interior 24 x 18 with wide aisles; containers spread over THREE walls at
///    3-unit spacing (a gap between every counter), open cook island in the
///    middle, sparse varied dressing. No repeated prop units.
///  • Game counters are ALWAYS instantiated fresh from the _New prefabs
///    (Assets/Prefabs/Counters/…). All pre-existing counters are parked at
///    x=40 (disabled) because old scene counters carried hand-placed decor.
///    Counters created by this builder live under "== GAME COUNTERS ==" and are
///    reused on re-runs, so re-running never duplicates.
///  • Sweeps loose KayKit env instances; never touches player/cameras/UI.
///  • Invisible bounds colliders on empty GameObjects only.
///
/// Layout (top-down):
///    N wall  : 7 fish containers, fridge in NW corner, sink decor in NE corner
///    W wall  : 3 shellfish containers          E wall : 3 mollusk containers
///    centre  : cook island — CUTTING + STOVE side by side, walk around it
///    divider : TRASH | bar | DELIVERY | bar | PLATES, walk gaps at both ends
///    S side  : dining — bar stools, two tables, order window + door in wall
/// </summary>
public static class RestaurantSceneBuilder
{
    private const string KayKitDir = "Assets/KayKit/Packs/Bits/KayKit - Restaurant Bits (For Unity)/Prefabs";
    private const string KayKitRoot = "Assets/KayKit";
    private const string CounterDir = "Assets/Prefabs/Counters";
    private const string SoDir = "Assets/_FishGame/KitchenObjectSO";

    private const string EnvRootName = "== RESTAURANT ENV ==";
    private const string BoundsRootName = "== RESTAURANT BOUNDS ==";
    private const string CountersRootName = "== GAME COUNTERS ==";
    private const string ParkedRootName = "== OLD COUNTERS (parked, disabled) ==";

    // interior half-extents; walls sit on these lines
    // NOTE: KayKit "_decorated" wall/table pieces are single meshes with furniture
    // baked in (stove+cabinet+hood+food) — never use them for plain surfaces.
    private const float HalfW = 14f;
    private const float HalfD = 10f;
    private const float DividerZ = -3f;   // sushi-bar line between kitchen and dining

    private static readonly Dictionary<string, GameObject> prefabCache = new();
    private static readonly Dictionary<string, Bounds> boundsCache = new();

    // ── the 13 containers: SO name, x, z, yRotation ─────────────────────────
    // North wall: fish & cephalopods (3-unit spacing, gap between each)
    // West wall: shellfish. East wall: mollusks.
    private static readonly (string so, float x, float z, float rot)[] ContainerSlots =
    {
        ("Salmon_Whole",  -9, 9, 0), ("Tuna_Whole",   -6, 9, 0), ("SeaBass_Whole", -3, 9, 0),
        ("Sardine_Whole",  0, 9, 0), ("Squid_Whole",   3, 9, 0), ("Octopus_Whole",  6, 9, 0),
        ("Shrimp_Whole",   9, 9, 0),

        ("Lobster_Whole", -13, 7, 270), ("Lobster2_Whole", -13, 4, 270), ("Crab_Whole", -13, 1, 270),

        ("Mussel_Closed",  13, 7, 90), ("Oyster_Closed",  13, 4, 90), ("Scallop_Closed", 13, 1, 90),
    };

    [MenuItem("Tide & Table/Build Restaurant")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Restaurant" &&
            !EditorUtility.DisplayDialog("Build Restaurant",
                $"Active scene is '{scene.name}', not 'Restaurant'. Build here anyway?", "Build", "Cancel"))
        {
            return;
        }

        prefabCache.Clear();
        boundsCache.Clear();
        Undo.SetCurrentGroupName("Build Restaurant");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            EditorUtility.DisplayProgressBar("Build Restaurant", "Sweeping old environment…", 0.05f);
            DestroyIfExists(EnvRootName);
            DestroyIfExists(BoundsRootName);
            int swept = SweepLooseKayKit(scene);

            Transform env = NewRoot(EnvRootName);

            EditorUtility.DisplayProgressBar("Build Restaurant", "Floors…", 0.15f);
            BuildFloors(env);

            EditorUtility.DisplayProgressBar("Build Restaurant", "Walls…", 0.3f);
            BuildWalls(env);

            EditorUtility.DisplayProgressBar("Build Restaurant", "Dressing…", 0.5f);
            BuildDressing(env);

            EditorUtility.DisplayProgressBar("Build Restaurant", "Game counters…", 0.75f);
            PlaceGameCounters();

            EditorUtility.DisplayProgressBar("Build Restaurant", "Bounds & player…", 0.9f);
            BuildBounds();
            EnsurePlayerInside();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"<color=cyan>[Tide & Table]</color> Restaurant v2 built (swept {swept} old KayKit pieces). " +
                      "Old counters are parked disabled under '" + ParkedRootName + "' — delete that root once happy. " +
                      "Re-run any time; it will not duplicate counters.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem("Tide & Table/Clear Restaurant Build")]
    public static void Clear()
    {
        DestroyIfExists(EnvRootName);
        DestroyIfExists(BoundsRootName);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Tide & Table] Restaurant environment cleared (game counters untouched).");
    }

    #region Floors & walls

    private static void BuildFloors(Transform env)
    {
        Transform floors = Group(env, "Floors");
        // kitchen checkerboard: from one tile south of the divider up to the north wall
        TileRegion("floor_kitchen", new Rect(-HalfW, DividerZ - 1f, HalfW * 2f, HalfD - (DividerZ - 1f)), floors);
        // dining: styleB
        TileRegion("floor_kitchen_styleB", new Rect(-HalfW, -HalfD, HalfW * 2f, (DividerZ - 1f) - (-HalfD)), floors);

        Bounds small = Measure("floor_kitchen_small");
        Place("floor_kitchen_small", new Vector3(9f, 0.01f, -HalfD - small.size.z * 0.5f + 0.05f), 0, floors);
    }

    private static void TileRegion(string key, Rect area, Transform parent)
    {
        Bounds b = Measure(key);
        float sx = Mathf.Max(0.5f, b.size.x);
        float sz = Mathf.Max(0.5f, b.size.z);
        int nx = Mathf.Max(1, Mathf.FloorToInt(area.width / sx + 0.01f));
        int nz = Mathf.Max(1, Mathf.FloorToInt(area.height / sz + 0.01f));
        float ox = area.xMin + (area.width - nx * sx) * 0.5f + sx * 0.5f;
        float oz = area.yMin + (area.height - nz * sz) * 0.5f + sz * 0.5f;
        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < nz; j++)
            {
                Place(key, new Vector3(ox + i * sx, 0.01f, oz + j * sz), 0, parent);
            }
        }
    }

    private static void BuildWalls(Transform env)
    {
        Transform walls = Group(env, "Walls");

        // PLAIN walls only — the "_decorated" variants are furnished units (see note above).

        // north (faces south)
        FillWallLine(new Vector3(-HalfW, 0, HalfD), new Vector3(HalfW, 0, HalfD), 180, _ => "wall", walls);

        // south: order window (west), curtained windows, doorway + door (east)
        FillWallLine(new Vector3(-HalfW, 0, -HalfD), new Vector3(HalfW, 0, -HalfD), 0, p =>
        {
            if (Mathf.Abs(p.x - 9f) < 1.05f) return "wall_doorway";
            if (Mathf.Abs(p.x + 9f) < 1.05f) return "wall_orderwindow";
            if (Mathf.Abs(p.x + 3f) < 1.05f || Mathf.Abs(p.x - 3f) < 1.05f) return "wall_window_closed_curtains_green";
            return "wall";
        }, walls);

        // west (faces east)
        FillWallLine(new Vector3(-HalfW, 0, -HalfD + 0.3f), new Vector3(-HalfW, 0, HalfD - 0.3f), 90, _ => "wall", walls);

        // east (faces west)
        FillWallLine(new Vector3(HalfW, 0, -HalfD + 0.3f), new Vector3(HalfW, 0, HalfD - 0.3f), 270, _ => "wall", walls);
    }

    private static void FillWallLine(Vector3 a, Vector3 b, float rotY, Func<Vector3, string> pick, Transform parent)
    {
        float unit = Measure("wall").size.x;
        if (unit < 0.25f) unit = 2f;
        float len = Vector3.Distance(a, b);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / unit));
        float stride = len / n;
        Vector3 dir = (b - a).normalized;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = a + dir * ((i + 0.5f) * stride);
            string key = pick(p);
            Place(key, p, rotY, parent);
            if (key == "wall_doorway")
            {
                Place("door_A", p, rotY, parent);
            }
        }
    }

    #endregion

    #region Dressing (sparse, varied)

    private static void BuildDressing(Transform env)
    {
        Transform kitchen = Group(env, "Kitchen Decor");
        Transform dining = Group(env, "Dining Decor");

        // corner anchors — one of each, no repetition (plain variants only)
        Place("fridge_A", new Vector3(-12.8f, 0f, 9f), 135, kitchen);                    // NW corner, angled
        GameObject sink = Place("kitchencounter_sink", new Vector3(12.8f, 0f, 9f), 225, kitchen); // NE corner, angled
        if (sink != null)
        {
            PlaceOnTop("dishrack_plates", "kitchencounter_sink", sink, new Vector2(-0.1f, -0.1f), 225, kitchen);
        }

        // sushi-bar divider decor (game counters fill the rest of the line)
        Transform bar = Group(env, "Sushi Bar");
        foreach (float x in new[] { -7f, -5f, -3f, 3f, 5f })
        {
            Place("kitchencounter_straight_A", new Vector3(x, 0f, DividerZ), 180, bar);
        }
        float barTop = Measure("kitchencounter_straight_A").max.y;
        Place("jar_A_medium", new Vector3(-5.2f, barTop, DividerZ), 10, bar);
        Place("jar_B_small", new Vector3(-4.7f, barTop, DividerZ + 0.15f), 0, bar);
        Place("ketchup", new Vector3(3.1f, barTop, DividerZ), 0, bar);
        Place("mustard", new Vector3(3.5f, barTop, DividerZ + 0.12f), 0, bar);
        Place("menu", new Vector3(5f, barTop, DividerZ), 200, bar);

        // stools on the dining side of the bar
        foreach (float x in new[] { -7f, -3f, 3f, 6f })
        {
            Place("chair_stool", new Vector3(x, 0f, DividerZ - 1.7f), 0, dining);
        }

        // dining tables — plain cloth tables, dressed by hand with fish-friendly props
        GameObject bigTable = Place("table_round_B_tablecloth_red", new Vector3(-8f, 0f, -7f), 0, dining);
        if (bigTable != null)
        {
            Place("chair_A", new Vector3(-9.6f, 0f, -7f), 90, dining);
            Place("chair_A", new Vector3(-6.4f, 0f, -7f), 270, dining);
            PlaceOnTop("stew_bowl", "table_round_B_tablecloth_red", bigTable, new Vector2(0f, 0.1f), 0, dining);
            PlaceOnTop("plate_small", "table_round_B_tablecloth_red", bigTable, new Vector2(-0.45f, -0.25f), 10, dining);
        }
        GameObject smallTable = Place("table_round_A_small", new Vector3(7f, 0f, -7f), 0, dining);
        if (smallTable != null)
        {
            Place("chair_stool", new Vector3(5.7f, 0f, -7f), 0, dining);
            Place("chair_stool", new Vector3(8.3f, 0f, -7f), 0, dining);
            PlaceOnTop("bowl_small", "table_round_A_small", smallTable, new Vector2(0f, 0f), 0, dining);
        }

        // a single crate corner in dining (deliveries waiting)
        GameObject crate = Place("crate", new Vector3(-13f, 0f, -9f), 15, dining);
        if (crate != null)
        {
            PlaceOnTop("crate_lid", "crate", crate, Vector2.zero, 15, dining);
        }
        Place("pot_large", new Vector3(-11.5f, 0f, -9.2f), 0, dining);
    }

    #endregion

    #region Game counters

    private static void PlaceGameCounters()
    {
        Transform countersRoot = Group(null, CountersRootName);

        // park every counter that this builder didn't create — old scene counters
        // carry hand-placed decor as children, which is what cluttered v1.
        ParkForeign<ContainerCounter>(countersRoot);
        ParkForeign<CuttingCounter>(countersRoot);
        ParkForeign<StoveCounter>(countersRoot);
        ParkForeign<PlatesCounter>(countersRoot);
        ParkForeign<DeliveryCounter>(countersRoot);

        // stations: cook island in the centre, delivery/plates in the divider
        EnsureStation<CuttingCounter>("CuttingCounter_New", new Vector3(-1.5f, 0f, 3.5f), 0, countersRoot);
        EnsureStation<StoveCounter>("StoveCounter_New", new Vector3(1.5f, 0f, 3.5f), 0, countersRoot);
        EnsureStation<DeliveryCounter>("DeliveryCounter_New", new Vector3(0f, 0f, DividerZ), 0, countersRoot);
        EnsureStation<PlatesCounter>("PlatesCounter_New", new Vector3(8f, 0f, DividerZ), 0, countersRoot);

        // trash: reuse the first one found anywhere (not in the required list), park extras
        var trashes = FindAll<TrashCounter>();
        for (int i = 0; i < trashes.Count; i++)
        {
            if (i == 0) { Snap(trashes[i].gameObject, new Vector3(-9f, 0f, DividerZ), 0, countersRoot); }
            else if (!IsParked(trashes[i].transform)) { Park(trashes[i].gameObject); }
        }

        // 13 fresh containers from the prefab, reusing only builder-owned instances
        GameObject containerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CounterDir}/ContainerCounter_New.prefab");
        var mine = new List<ContainerCounter>();
        foreach (var c in FindAll<ContainerCounter>())
        {
            if (c.transform.parent == countersRoot) { mine.Add(c); }
        }
        mine.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < ContainerSlots.Length; i++)
        {
            var slot = ContainerSlots[i];
            GameObject counter;
            if (i < mine.Count)
            {
                counter = mine[i].gameObject;
            }
            else if (containerPrefab != null)
            {
                counter = (GameObject)PrefabUtility.InstantiatePrefab(containerPrefab);
                Undo.RegisterCreatedObjectUndo(counter, "Build Restaurant");
            }
            else
            {
                Debug.LogWarning("[Tide & Table] ContainerCounter_New.prefab not found — cannot create containers.");
                break;
            }

            Snap(counter, new Vector3(slot.x, 0f, slot.z), slot.rot, countersRoot);

            var so = AssetDatabase.LoadAssetAtPath<KitchenObjectSO>($"{SoDir}/{slot.so}.asset");
            if (so == null)
            {
                Debug.LogWarning($"[Tide & Table] Missing SO {slot.so} — run 'Generate Fish Content' first.");
                continue;
            }
            var comp = counter.GetComponent<ContainerCounter>();
            var serialized = new SerializedObject(comp);
            SerializedProperty prop = serialized.FindProperty("kitchenObject");
            if (prop != null)
            {
                prop.objectReferenceValue = so;
                serialized.ApplyModifiedProperties();
            }
            Undo.RecordObject(counter, "rename");
            counter.name = slot.so.Replace("_Whole", "").Replace("_Closed", "") + " CC";
        }

        for (int i = ContainerSlots.Length; i < mine.Count; i++)
        {
            Park(mine[i].gameObject);
        }
    }

    /// <summary>Parks all instances of T that are not children of the builder's counters root.</summary>
    private static void ParkForeign<T>(Transform countersRoot) where T : KitchenCounter
    {
        foreach (var c in FindAll<T>())
        {
            if (c.GetType() != typeof(T)) { continue; }          // exact type only (CuttingCounter is a ClearCounter…)
            if (c.transform.parent == countersRoot) { continue; } // builder-owned, clean
            if (IsParked(c.transform)) { continue; }
            Park(c.gameObject);
        }
    }

    private static void EnsureStation<T>(string prefabName, Vector3 pos, float rotY, Transform countersRoot) where T : KitchenCounter
    {
        GameObject chosen = null;
        foreach (var c in FindAll<T>())
        {
            if (c.GetType() == typeof(T) && c.transform.parent == countersRoot)
            {
                chosen = c.gameObject;
                break;
            }
        }

        if (chosen == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CounterDir}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Tide & Table] {prefabName}.prefab not found.");
                return;
            }
            chosen = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(chosen, "Build Restaurant");
        }

        Snap(chosen, pos, rotY, countersRoot);
    }

    private static bool IsParked(Transform t)
    {
        return t.parent != null && t.parent.name == ParkedRootName;
    }

    private static void Snap(GameObject go, Vector3 pos, float rotY, Transform parent)
    {
        Undo.RecordObject(go.transform, "Build Restaurant");
        if (go.transform.parent != parent) { Undo.SetTransformParent(go.transform, parent, "Build Restaurant"); }
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        if (!go.activeSelf) { go.SetActive(true); }
    }

    private static void Park(GameObject go)
    {
        Transform parking = Group(null, ParkedRootName);
        Undo.RecordObject(go.transform, "Build Restaurant");
        Undo.SetTransformParent(go.transform, parking, "Build Restaurant");
        go.transform.position = new Vector3(40f, 0f, parking.childCount * 3f);
        Undo.RecordObject(go, "Build Restaurant");
        go.SetActive(false);
        Debug.Log($"[Tide & Table] Parked old counter '{go.name}' (disabled). Delete '{ParkedRootName}' once happy.");
    }

    #endregion

    #region Bounds & player

    private static void BuildBounds()
    {
        Transform root = NewRoot(BoundsRootName);
        AddBox(root, "North", new Vector3(0, 1.25f, HalfD + 0.25f), new Vector3(HalfW * 2 + 1.5f, 2.5f, 0.5f));
        AddBox(root, "South", new Vector3(0, 1.25f, -HalfD - 0.25f), new Vector3(HalfW * 2 + 1.5f, 2.5f, 0.5f));
        AddBox(root, "East", new Vector3(HalfW + 0.25f, 1.25f, 0), new Vector3(0.5f, 2.5f, HalfD * 2 + 1.5f));
        AddBox(root, "West", new Vector3(-HalfW - 0.25f, 1.25f, 0), new Vector3(0.5f, 2.5f, HalfD * 2 + 1.5f));
        // divider bar decor pieces (trash/delivery/plates counters have their own colliders);
        // walk gaps stay open at x = ±(10..14)
        AddBox(root, "BarWest", new Vector3(-5.5f, 1.25f, DividerZ), new Vector3(9f, 2.5f, 0.9f));
        AddBox(root, "BarEast", new Vector3(5.5f, 1.25f, DividerZ), new Vector3(9f, 2.5f, 0.9f));
    }

    private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Restaurant");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
    }

    private static void EnsurePlayerInside()
    {
        var players = FindAll<Player>();
        if (players.Count == 0) { return; }
        Transform p = players[0].transform;
        Vector3 pos = p.position;
        if (Mathf.Abs(pos.x) > HalfW - 0.8f || Mathf.Abs(pos.z) > HalfD - 0.8f)
        {
            Undo.RecordObject(p, "Build Restaurant");
            p.position = new Vector3(0f, pos.y, 0f);
            Debug.Log("[Tide & Table] Player was outside the restaurant — moved to the kitchen centre.");
        }
    }

    #endregion

    #region Sweep

    private static int SweepLooseKayKit(Scene scene)
    {
        var doomed = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectKayKit(root, doomed);
        }
        foreach (GameObject go in doomed)
        {
            Undo.DestroyObjectImmediate(go);
        }
        return doomed.Count;
    }

    private static void CollectKayKit(GameObject go, List<GameObject> doomed)
    {
        if (go.GetComponent<KitchenCounter>() != null) { return; } // counters keep their internal KayKit bits

        if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && go.GetComponentInChildren<KitchenCounter>(true) == null)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (!string.IsNullOrEmpty(path) && path.StartsWith(KayKitRoot, StringComparison.OrdinalIgnoreCase))
            {
                doomed.Add(go);
                return;
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            CollectKayKit(go.transform.GetChild(i).gameObject, doomed);
        }
    }

    #endregion

    #region Helpers

    private static List<T> FindAll<T>() where T : Component
    {
        return new List<T>(Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    private static Transform NewRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Restaurant");
        return go.transform;
    }

    private static void DestroyIfExists(string rootName)
    {
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }

    private static Transform Group(Transform parent, string name)
    {
        if (parent == null)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) { return found.transform; }
            return NewRoot(name);
        }
        Transform t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build Restaurant");
            t = go.transform;
            t.SetParent(parent, false);
        }
        return t;
    }

    private static GameObject LoadKayKit(string key)
    {
        if (prefabCache.TryGetValue(key, out GameObject cached)) { return cached; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KayKitDir}/{key}.prefab");
        if (prefab == null) { Debug.LogWarning($"[Tide & Table] Missing KayKit prefab: {key}"); }
        prefabCache[key] = prefab;
        return prefab;
    }

    private static Bounds Measure(string key)
    {
        if (boundsCache.TryGetValue(key, out Bounds cached)) { return cached; }
        Bounds b = new Bounds(Vector3.zero, Vector3.one);
        GameObject prefab = LoadKayKit(key);
        if (prefab != null)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.transform.position = new Vector3(0, -2000f, 0);
            var rs = temp.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                foreach (Renderer r in rs) { b.Encapsulate(r.bounds); }
                b.center -= temp.transform.position;
            }
            Object.DestroyImmediate(temp);
        }
        boundsCache[key] = b;
        return b;
    }

    private static GameObject Place(string key, Vector3 pos, float rotY, Transform parent)
    {
        GameObject prefab = LoadKayKit(key);
        if (prefab == null) { return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        Undo.RegisterCreatedObjectUndo(go, "Build Restaurant");
        return go;
    }

    private static void PlaceOnTop(string key, string baseKey, GameObject basePiece, Vector2 xzOffset, float rotY, Transform parent)
    {
        float topY = basePiece.transform.position.y + Measure(baseKey).max.y;
        Vector3 pos = basePiece.transform.position + new Vector3(xzOffset.x, 0, xzOffset.y);
        pos.y = topY;
        Place(key, pos, rotY, parent);
    }

    #endregion
}
