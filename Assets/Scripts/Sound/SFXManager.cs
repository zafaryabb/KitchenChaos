using System;
using UnityEngine;

public class SFXManager : MonoBehaviour
{
    private const string PLAYER_PREF_SFX_VOLUME = "SFXVolumeLevel";

    public event EventHandler<int> OnSFXVolumeChanged;

    public static SFXManager Instance { get; private set; }

    [SerializeField] private SFXSO sfx;
    [SerializeField] private int defaultVolumeLevel = 10;
    [SerializeField] private int maxVolumeLevel = 10;

    private bool movingSync = true;   // true on left foot, false on right foot
    private int MovingSFXIndex
    {
        get
        {
            movingSync = !movingSync;
            return movingSync ? 0 : 1;
        }
    }

    private int volumeLevel;
    private float Volume => volumeLevel * 0.1f;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Cannot have mutiple instance of SFXManager");
        }
        Instance = this;
        volumeLevel = PlayerPrefs.GetInt(PLAYER_PREF_SFX_VOLUME, defaultVolumeLevel);
    }

    private void Start()
    {
        DeliveryManager.Instance.OnOrderDelivered += (obj, _) => PlaySFX(sfx.deliverySuccess, DeliveryManager.Instance.transform.position);
        DeliveryManager.Instance.OnFailedOrderDeliver += (obj, _) => PlaySFX(sfx.deliveryFail, DeliveryManager.Instance.transform.position);
        CuttingCounter.OnAnyCut += (obj, _) => PlaySFX(FirstNonEmpty(sfx.slice, sfx.chop), (obj as CuttingCounter).transform.position);
        CuttingCounter.OnAnyPerfectSlice += (obj, _) => PlaySFX(sfx.slicePerfect, (obj as CuttingCounter).transform.position);
        CatStationCounter.OnAnyPetFed += CatStationCounter_OnAnyPetFed;
        Player.Instance.OnPlayerPickedUp += (obj, _) => PlaySFX(sfx.objectPickup, (obj as Player).transform.position);
        PlateKitchenObject.OnAnyIngredientAdded += (obj, _) => PlaySFX(sfx.objectPickup, (obj as PlateKitchenObject).transform.position);
        HolderCounter.OnAnyItemPlaced += (obj, _) => PlaySFX(sfx.objectDrop, (obj as HolderCounter).transform.position);
        TrashCounter.OnAnyItemTrashed += (obj, _) => PlaySFX(sfx.trash, (obj as TrashCounter).transform.position);
    }

    private void CatStationCounter_OnAnyPetFed(object sender, int e)
    {
        Vector3 position = (sender as CatStationCounter).transform.position;
        PlaySFX(sfx.petEat, position);
        PlaySFX(sfx.petPurr, position);
    }

    public void PlayWarningSound(Vector3 position, float volumeMultipler = 1f)
    {
        PlaySFX(SafeIndex(sfx.warn, 1), position, volumeMultipler);
    }

    public void PlayCountdownSound(float volumeMultipler = 1f)
    {
        PlaySFX(SafeIndex(sfx.warn, 0), Camera.main.transform.position, volumeMultipler);
    }

    public void PlayWalkingSound(Vector3 position, float volumeMultipler = 1f)
    {
        PlaySFX(SafeIndex(sfx.walk, MovingSFXIndex), position, volumeMultipler);
    }

    public void PlaySprintingSound(Vector3 position, float volumeMultipler = 1f)
    {
        PlaySFX(SafeIndex(sfx.sprint, MovingSFXIndex), position, volumeMultipler);
    }

    private void PlaySFX(AudioClip[] clip, Vector3 position, float volumeMultipler = 1f)
    {
        if (clip == null || clip.Length == 0)
        {
            return;
        }
        PlaySFX(clip[UnityEngine.Random.Range(0, clip.Length)], position, volumeMultipler);
    }

    private void PlaySFX(AudioClip clip, Vector3 position, float volumeMultipler = 1f)
    {
        if (clip == null)
        {
            // clip not wired up yet — stay silent rather than throwing
            return;
        }
        AudioSource.PlayClipAtPoint(clip, position, Volume * volumeMultipler);
    }

    /// <summary>Returns the first array that actually has clips, so new sounds can fall back to old ones.</summary>
    private static AudioClip[] FirstNonEmpty(AudioClip[] primary, AudioClip[] fallback)
    {
        return (primary != null && primary.Length > 0) ? primary : fallback;
    }

    private static AudioClip SafeIndex(AudioClip[] clips, int index)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }
        return clips[Mathf.Clamp(index, 0, clips.Length - 1)];
    }

    public void IncreaseVolumeLevel()
    {
        volumeLevel = (volumeLevel + 1) % (maxVolumeLevel + 1);
        OnSFXVolumeChanged?.Invoke(this, volumeLevel);
        PlayerPrefs.SetInt(PLAYER_PREF_SFX_VOLUME, volumeLevel);
    }

    public int GetVolumeLevel()
    {
        return volumeLevel;
    }
}
