using UnityEngine;

[CreateAssetMenu()]
public class CuttingRecipeSO : ScriptableObject
{
    /// <summary>Cosmetic flavour used to pick SFX / animation variety.</summary>
    public enum ProcessVerb
    {
        Cut,
        Fillet,
        Slice,
        Peel,
        Shuck,
        Ring,
        Chop,
    }

    public KitchenObjectSO from;
    public KitchenObjectSO to;
    public int cutCount = 3;

    [Tooltip("Optional leftover produced when this stage finishes (e.g. a fish skeleton for the cats). " +
             "Spawned onto the cutting station's byproduct output if one is assigned and free.")]
    public KitchenObjectSO byproduct;

    [Tooltip("Purely cosmetic: selects which slicing sound / animation flavour to use.")]
    public ProcessVerb verb = ProcessVerb.Cut;
}
