using UnityEngine;

[CreateAssetMenu()]
public class SFXSO : ScriptableObject
{
    public AudioClip[] chop;
    public AudioClip[] deliveryFail;
    public AudioClip[] deliverySuccess;
    public AudioClip[] walk;
    public AudioClip[] sprint;
    public AudioClip[] objectDrop;
    public AudioClip[] objectPickup;
    public AudioClip panSizzle;
    public AudioClip[] trash;
    public AudioClip[] warn;

    [Header("Fish restaurant (wire up later)")]
    [Tooltip("ASMR slice on a normal cut. Falls back to 'chop' if empty.")]
    public AudioClip[] slice;
    [Tooltip("Bright sparkle/chime on a perfect slice.")]
    public AudioClip[] slicePerfect;
    public AudioClip[] peel;
    public AudioClip[] shuck;
    [Tooltip("Played when the pet is fed (munch).")]
    public AudioClip[] petEat;
    [Tooltip("Soft, looping-style purr / happy reaction when the pet is fed.")]
    public AudioClip[] petPurr;
}
