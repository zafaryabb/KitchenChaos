using Cinemachine;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Blends the camera into a calm, hand-composed close-up of the cutting board
/// whenever the player stands at a cutting station holding something cuttable
/// (or is mid-cut), and blends back out when they step away.
///
/// IMPORTANT: this director does NOT drive the prep camera's position/rotation
/// procedurally — the shot is whatever you compose on the vcam in the Scene view.
/// That keeps the angle stable and fully under your control. To adjust the shot,
/// just move/rotate the Prep vcam (or, for per-station shots, assign a
/// <c>cameraPose</c> on each CuttingCounter and the director snaps to it).
///
/// Scene setup:
///  - CinemachineBrain on the Main Camera.
///  - A gameplay vcam (Priority 10) at your normal kitchen angle.
///  - A "Prep" vcam assigned to <see cref="prepCamera"/>, with **Body = Do Nothing**
///    and **Aim = Do Nothing**, positioned by hand over the chef's shoulder.
///  - (Optional) a global post-process <see cref="prepVolume"/> with Depth of Field;
///    its weight is eased 0..1 so the kitchen softly blurs away.
/// </summary>
public class PrepCameraDirector : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera prepCamera;
    [SerializeField] private int engagedPriority = 20;
    [SerializeField] private int disengagedPriority = 0;

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

        if (shouldEngage && prepCamera != null)
        {
            // If this station provides a composed pose, snap the (static) prep
            // vcam to it so different boards can have different shots. Otherwise
            // we leave the vcam exactly as you composed it.
            Transform pose = station.GetCameraPose();
            if (pose != null)
            {
                prepCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        SetEngaged(shouldEngage);
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
