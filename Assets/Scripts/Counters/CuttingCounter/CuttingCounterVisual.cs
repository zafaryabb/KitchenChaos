using UnityEngine;
using DG.Tweening;

/// <summary>
/// Reacts to cutting: triggers the knife animation, gives the food a satisfying
/// little squash-and-settle, puffs slice particles, and adds a sparkle on a
/// perfect slice. All extra references are optional and null-guarded so the
/// counter still works before the juice is wired up.
/// </summary>
public class CuttingCounterVisual : MonoBehaviour
{
    private const string CUT = "Cut";

    [Header("Optional juice (assign in prefab)")]
    [Tooltip("Transform of the food sitting on the board; gets a squash punch per cut. Usually the counter's item spawn point.")]
    [SerializeField] private Transform itemAnchor;
    [SerializeField] private ParticleSystem sliceParticles;
    [SerializeField] private ParticleSystem perfectSparkle;
    [SerializeField] private float punchScale = 0.15f;
    [SerializeField] private float punchDuration = 0.18f;

    private Animator animator;
    private CuttingCounter cuttingCounter;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cuttingCounter = GetComponentInParent<CuttingCounter>();
    }

    private void Start()
    {
        cuttingCounter.OnCut += CuttingCounter_OnCut;
    }

    private void CuttingCounter_OnCut(object sender, CuttingCounter.CutProgressUpdatedArg e)
    {
        if (!e.active)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(CUT);
        }

        if (sliceParticles != null)
        {
            sliceParticles.Play();
        }

        if (itemAnchor != null)
        {
            itemAnchor.DOComplete();
            itemAnchor.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.6f);
        }

        if (e.perfect && perfectSparkle != null)
        {
            perfectSparkle.Play();
        }
    }
}
