using System;
using UnityEngine;

/// <summary>
/// A cosy corner counter where the player drops scraps (fish skeletons, crab
/// shells) for the resident pet. Accepts only items flagged as
/// <see cref="KitchenObjectSO.petFood"/>; everything else is gently refused.
/// On a successful feed it plays the pet reaction and ticks the zen score.
/// </summary>
public class CatStationCounter : KitchenCounter
{
    public static event EventHandler<int> OnAnyPetFed;

    public static void ResetStaticData()
    {
        OnAnyPetFed = null;
    }

    [SerializeField] private PetController pet;

    protected override void InteractAction(Player player)
    {
        if (!player.HoldingObject)
        {
            return;
        }

        KitchenObject held = player.GetCurrentKitchenObject();
        if (held.GetKitchenObjectSO() == null || !held.GetKitchenObjectSO().petFood)
        {
            Debug.Log("The cat only wants the scraps (skeletons, shells).");
            return;
        }

        held.DestroySelf();

        if (pet != null)
        {
            pet.Feed();
        }

        if (ZenScoreManager.Instance != null)
        {
            ZenScoreManager.Instance.AddPetFed();
        }

        OnAnyPetFed?.Invoke(this, 1);
    }
}
