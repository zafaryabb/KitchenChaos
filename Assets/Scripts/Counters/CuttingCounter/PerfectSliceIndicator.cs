using UnityEngine;

/// <summary>
/// Visualises the gentle "perfect slice" sweet-spot for a cutting station.
/// A marker sweeps back and forth along a track; landing a cut while the marker
/// overlaps the highlighted sweet-spot zone earns a perfect slice (a sparkle and
/// a small bonus) — but missing never punishes.
///
/// This only drives transforms, so it works for either a world-space rig above
/// the board or a screen-space UI bar. Assign:
///  - <see cref="marker"/>: moved along local X between -<see cref="trackHalfWidth"/>..+.
///  - <see cref="sweetZone"/>: positioned/scaled to show the target window.
///  - <see cref="root"/>: shown only while a cut is in progress (optional).
/// </summary>
public class PerfectSliceIndicator : MonoBehaviour
{
    [SerializeField] private CuttingCounter cuttingCounter;
    [SerializeField] private GameObject root;
    [SerializeField] private Transform marker;
    [SerializeField] private Transform sweetZone;
    [SerializeField] private float trackHalfWidth = 0.5f;

    private void Awake()
    {
        if (cuttingCounter == null)
        {
            cuttingCounter = GetComponentInParent<CuttingCounter>();
        }
    }

    private void Start()
    {
        if (sweetZone != null && cuttingCounter != null)
        {
            // place + size the sweet-spot zone once from the counter's settings
            float center = (cuttingCounter.PerfectTargetCenter * 2f - 1f) * trackHalfWidth;
            float width = cuttingCounter.PerfectWindow * 2f * (trackHalfWidth * 2f);

            Vector3 p = sweetZone.localPosition;
            p.x = center;
            sweetZone.localPosition = p;

            Vector3 s = sweetZone.localScale;
            s.x = Mathf.Max(0.0001f, width);
            sweetZone.localScale = s;
        }
    }

    private void Update()
    {
        bool active = cuttingCounter != null && cuttingCounter.IsCutting && cuttingCounter.PerfectSliceEnabled;

        if (root != null && root.activeSelf != active)
        {
            root.SetActive(active);
        }

        if (active && marker != null)
        {
            // phase 0..1 -> ping-pong across the track for a calm back-and-forth sweep
            float t = Mathf.PingPong(cuttingCounter.RhythmPhase * 2f, 1f);
            Vector3 p = marker.localPosition;
            p.x = Mathf.Lerp(-trackHalfWidth, trackHalfWidth, t);
            marker.localPosition = p;
        }
    }
}
