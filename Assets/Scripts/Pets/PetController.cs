using UnityEngine;
using DG.Tweening;

/// <summary>
/// Drives a Quirky Series pet (Cat / Dog) animator for the calm feeding moment.
/// State names default to the AC_Cat / AC_Dog controllers (Idle_A, Eat, Eyes_Happy).
/// All animator calls are state-name based, so it works without knowing the
/// controller's parameter setup. Everything is null-guarded so an unwired pet
/// simply does nothing instead of erroring.
/// </summary>
public class PetController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Body states (layer 0)")]
    [SerializeField] private string idleState = "Idle_A";
    [SerializeField] private string eatState = "Eat";
    [SerializeField] private string bounceState = "Bounce";

    [Header("Eyes states (layer 1, optional)")]
    [SerializeField] private int eyesLayer = 1;
    [SerializeField] private string happyEyesState = "Eyes_Happy";
    [SerializeField] private string idleEyesState = "Eyes_Blink";

    [Header("Timing")]
    [SerializeField] private float eatDuration = 2f;
    [SerializeField] private float crossFade = 0.15f;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        PlayIdle();
    }

    /// <summary>
    /// Play the happy "eat" reaction, then ease back to idle.
    /// </summary>
    public void Feed()
    {
        if (animator == null)
        {
            return;
        }

        animator.CrossFade(eatState, crossFade, 0);
        PlayEyes(happyEyesState);

        // a little delighted hop
        transform.DOKill(complete: true);
        transform.DOPunchPosition(Vector3.up * 0.12f, eatDuration * 0.5f, 5, 0.4f);

        CancelInvoke(nameof(PlayIdle));
        Invoke(nameof(PlayIdle), eatDuration);
    }

    private void PlayIdle()
    {
        if (animator == null)
        {
            return;
        }
        animator.CrossFade(idleState, crossFade, 0);
        PlayEyes(idleEyesState);
    }

    private void PlayEyes(string state)
    {
        if (animator == null || string.IsNullOrEmpty(state))
        {
            return;
        }
        if (eyesLayer >= 0 && animator.layerCount > eyesLayer)
        {
            animator.CrossFade(state, crossFade, eyesLayer);
        }
    }
}
