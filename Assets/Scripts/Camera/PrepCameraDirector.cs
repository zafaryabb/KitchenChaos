using Cinemachine;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Eases the camera into a calm over-the-shoulder close-up whenever the player
/// stands at a cutting station holding something cuttable (or is mid-cut), and
/// eases back out to the normal gameplay view when they step away.
///
/// Setup in the scene:
///  - A Cinemachine Brain on the Main Camera.
///  - A normal gameplay virtual camera (any priority, e.g. 10).
///  - A "Prep" virtual camera assigned to <see cref="prepCamera"/>. Give it a
///    Framing Transposer (body) + Composer (aim) so it frames nicely over the
///    chef's shoulder; this director sets its Follow/LookAt and raises its
///    priority when engaged.
///  - (Optional) a global post-process <see cref="prepVolume"/> holding Depth of
///    Field; its weight is eased 0..1 so the kitchen softly blurs away.
/// </summary>
public class PrepCameraDirector : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera prepCamera;
    [SerializeField] private int engagedPriority = 20;
    [SerializeField] private int disengagedPriority = 0;

    [Header("Over-the-shoulder framing")]
    [Tooltip("Camera follows this rig; if left empty it follows the player transform.")]
    [SerializeField] private Transform followOverride;

    [Header("Optional depth-of-field volume")]
    [SerializeField] private Volume prepVolume;
    [SerializeField] private float engagedVolumeWeight = 1f;
    [SerializeField] private float blendDuration = 0.6f;

    private bool engaged;
    private Tween volumeTween;

    private void Start()
    {
        if (prepCamera != null)
        {
            prepCamera.Priority = disengagedPriority;
        }
        if (prepVolume != null)
        {
            prepVolume.weight = 0f;
        }
    }

    private void Update()
    {
        CuttingCounter station = GetActiveCuttingStation();
        bool shouldEngage = station != null;

        if (shouldEngage)
        {
            if (prepCamera != null)
            {
                prepCamera.Follow = followOverride != null ? followOverride : Player.Instance.transform;
                prepCamera.LookAt = station.GetFocusPoint();
            }
            SetEngaged(true);
        }
        else
        {
            SetEngaged(false);
        }
    }

    private CuttingCounter GetActiveCuttingStation()
    {
        if (Player.Instance == null)
        {
            return null;
        }

        if (Player.Instance.GetSelectedCounter() is CuttingCounter cutting &&
            (cutting.HasCuttableItem || cutting.IsCutting))
        {
            return cutting;
        }
        return null;
    }

    private void SetEngaged(bool value)
    {
        if (engaged == value)
        {
            return;
        }
        engaged = value;

        if (prepCamera != null)
        {
            prepCamera.Priority = engaged ? engagedPriority : disengagedPriority;
        }

        if (prepVolume != null)
        {
            volumeTween?.Kill();
            volumeTween = DOTween.To(
                () => prepVolume.weight,
                w => prepVolume.weight = w,
                engaged ? engagedVolumeWeight : 0f,
                blendDuration);
        }
    }
}
