using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders every generated fish item prefab to a clean, transparent sprite and
/// assigns it to that item's <see cref="KitchenObjectSO.icon"/> — so order
/// tickets show real food art. Run it from the menu:
///   Tide &amp; Table ▸ Generate Item Icons
///
/// Notes:
///  - Works under URP: uses <c>RenderPipeline.SubmitRenderRequest</c> (Camera.Render
///    is unsupported in SRP) and reconstructs alpha from a black + white pass so the
///    background is truly transparent on any pipeline.
///  - Idempotent: re-run any time. Tweak the constants below (size / angle / padding)
///    and re-run to restyle every icon at once.
///  - Only touches SOs under <c>_FishGame/KitchenObjectSO</c>; burger content is left alone.
/// </summary>
public static class SpriteIconGenerator
{
    private const string SoDir = "Assets/_FishGame/KitchenObjectSO";
    private const string IconsDir = "Assets/_FishGame/Icons";

    // --- tweak these, then re-run ---
    private const int IconSize = 256;
    private static readonly Vector3 ViewAngle = new Vector3(20f, -30f, 0f); // nice 3/4 view
    private const float Padding = 1.15f;     // >1 leaves a margin around the item
    private const int IsolationLayer = 31;   // an unused layer for the capture rig

    [MenuItem("Tide & Table/Generate Item Icons")]
    public static void GenerateIcons()
    {
        EnsureFolder(IconsDir);

        string[] guids = AssetDatabase.FindAssets("t:KitchenObjectSO", new[] { SoDir });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[Tide & Table] No KitchenObjectSO found under {SoDir}. Run 'Generate Fish Content' first.");
            return;
        }

        int made = 0, skipped = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string soPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<KitchenObjectSO>(soPath);
                if (so == null || so.prefab == null)
                {
                    skipped++;
                    continue;
                }

                EditorUtility.DisplayProgressBar("Generating item icons", so.name, (float)i / guids.Length);

                Texture2D tex = Capture(so.prefab);
                if (tex == null)
                {
                    skipped++;
                    continue;
                }

                string pngPath = $"{IconsDir}/{so.name}.png";
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(pngPath);
                ConfigureSpriteImporter(pngPath);

                so.icon = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                EditorUtility.SetDirty(so);
                made++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=cyan>[Tide & Table]</color> Icons generated: {made}, skipped: {skipped}. Folder: <b>{IconsDir}</b>");
    }

    #region Rendering

    private static Texture2D Capture(GameObject prefab)
    {
        // Spawn the item far from any scene geometry, on an isolated layer.
        Vector3 origin = new Vector3(0f, 10000f, 0f);
        GameObject instance = Object.Instantiate(prefab, origin, Quaternion.identity);
        instance.hideFlags = HideFlags.HideAndDontSave;
        SetLayerRecursive(instance, IsolationLayer);

        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[Tide & Table] {prefab.name} has no renderers; skipped.");
            Object.DestroyImmediate(instance);
            return null;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Camera rig (orthographic for consistent framing)
        var camGo = new GameObject("IconCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.cullingMask = 1 << IsolationLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = true;

        float radius = Mathf.Max(0.0001f, bounds.extents.magnitude);
        cam.orthographicSize = radius * Padding;

        Quaternion rot = Quaternion.Euler(ViewAngle);
        Vector3 dir = rot * Vector3.forward;
        float dist = radius * 4f + 1f;
        camGo.transform.SetPositionAndRotation(bounds.center - dir * dist, rot);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = dist + radius * 4f + 10f;

        var urp = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urp != null)
        {
            urp.renderPostProcessing = false;
            urp.renderShadows = false;
        }

        // A fill light so the item reads well regardless of the scene's lighting.
        var lightGo = new GameObject("IconLight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.cullingMask = 1 << IsolationLayer;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

        Color[] black = RenderPass(cam, rt, Color.black);
        Color[] white = RenderPass(cam, rt, Color.white);

        // Reconstruct straight alpha + color from the two backgrounds:
        //   over black:  cb = c*a            -> c  = cb / a
        //   over white:  cw = c*a + (1-a)    -> a  = 1 - (cw - cb)
        var result = new Color[IconSize * IconSize];
        for (int i = 0; i < result.Length; i++)
        {
            Color cb = black[i];
            Color cw = white[i];
            float a = 1f - ((cw.r - cb.r) + (cw.g - cb.g) + (cw.b - cb.b)) / 3f;
            a = Mathf.Clamp01(a);
            result[i] = a > (1f / 255f)
                ? new Color(cb.r / a, cb.g / a, cb.b / a, a)
                : new Color(0f, 0f, 0f, 0f);
        }

        var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        tex.SetPixels(result);
        tex.Apply();

        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(lightGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(instance);
        return tex;
    }

    private static Color[] RenderPass(Camera cam, RenderTexture rt, Color background)
    {
        cam.backgroundColor = background;
        cam.targetTexture = rt;

        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            cam.Render(); // built-in pipeline fallback
        }

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var reader = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        reader.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
        reader.Apply();
        Color[] pixels = reader.GetPixels();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Object.DestroyImmediate(reader);
        return pixels;
    }

    #endregion

    #region Helpers

    private static void ConfigureSpriteImporter(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
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

    #endregion
}
