using System;
using UnityEngine;

/// <summary>
/// Tracks the calm, additive "score" for the soothing fish restaurant:
/// plates served, perfect slices, and pets fed. Never decreases, never fails.
/// UI can subscribe to <see cref="OnScoreChanged"/>.
/// </summary>
public class ZenScoreManager : MonoBehaviour
{
    public static ZenScoreManager Instance { get; private set; }

    public event EventHandler<ScoreSnapshot> OnScoreChanged;

    public class ScoreSnapshot : EventArgs
    {
        public int platesServed;
        public int perfectSlices;
        public int petsFed;
    }

    private int platesServed;
    private int perfectSlices;
    private int petsFed;

    public int PlatesServed => platesServed;
    public int PerfectSlices => perfectSlices;
    public int PetsFed => petsFed;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Cannot have multiple instance of ZenScoreManager");
        }
        Instance = this;
    }

    private void Start()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnOrderDelivered += (_, _) =>
            {
                ++platesServed;
                Raise();
            };
        }

        CuttingCounter.OnAnyPerfectSlice += (_, _) =>
        {
            ++perfectSlices;
            Raise();
        };

        CatStationCounter.OnAnyPetFed += (_, _) =>
        {
            ++petsFed;
            Raise();
        };
    }

    public void AddPetFed()
    {
        ++petsFed;
        Raise();
    }

    private void Raise()
    {
        OnScoreChanged?.Invoke(this, new ScoreSnapshot
        {
            platesServed = platesServed,
            perfectSlices = perfectSlices,
            petsFed = petsFed,
        });
    }
}
