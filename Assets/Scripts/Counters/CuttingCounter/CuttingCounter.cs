using System;
using UnityEngine;

/// <summary>
/// A counter that processes the held item stage by stage when alt-interacted.
/// Chains naturally (Whole -> Half -> Fillet -> Sashimi) because each recipe is
/// looked up by its <c>from</c> item, so the product of one stage becomes the
/// input of the next. A stage may also drop a byproduct (e.g. a skeleton) onto
/// an adjacent scrap tray, and supports the calm "perfect slice" sweet spot.
/// </summary>
public class CuttingCounter : ClearCounter, IProgressTracked
{
    public static event EventHandler OnAnyCut;
    public static event EventHandler OnAnyPerfectSlice;

    public static new void ResetStaticData()
    {
        OnAnyCut = null;
        OnAnyPerfectSlice = null;
    }

    public event EventHandler<CutProgressUpdatedArg> OnCut;
    public event EventHandler<IProgressTracked.ProgressChangedArg> OnProgressChanged;

    public class CutProgressUpdatedArg : EventArgs, IProgressTracked.ProgressChangedArg
    {
        public bool active;
        public bool perfect;
        public float progressNormalized;
        public CuttingRecipeSO.ProcessVerb verb;

        public bool IsBarActive()
        {
            return active;
        }

        public bool IsWarning()
        {
            return false;
        }
    }

    public float GetNormalizedProgress()
    {
        Debug.Assert(Cutting);
        return CuttingProgressNormalized;
    }

    [Header("Recipes")]
    [SerializeField] private CuttingRecipeSO[] cuttingRecipes;

    [Header("Byproduct output (e.g. skeleton tray for the cats)")]
    [Tooltip("Where stage byproducts are placed. If null or already occupied, the byproduct is skipped.")]
    [SerializeField] private HolderCounter byproductOutput;

    [Header("Perfect slice (the calm sweet spot)")]
    [SerializeField] private bool perfectSliceEnabled = true;
    [Tooltip("How fast the sweet-spot marker sweeps, in cycles per second. Keep it slow and calm.")]
    [SerializeField] private float rhythmSpeed = 0.8f;
    [Range(0f, 0.5f)]
    [Tooltip("Half-width of the sweet spot as a fraction of the sweep. Bigger = more forgiving.")]
    [SerializeField] private float perfectWindow = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float perfectTargetCenter = 0.5f;

    [Header("Camera focus")]
    [Tooltip("Where the over-the-shoulder prep camera looks. Defaults to the item spawn point.")]
    [SerializeField] private Transform focusPoint;

    private CuttingRecipeSO activeCuttingRecipe;
    private int cuttingProgress;
    private float rhythmPhase;

    private bool Cutting => activeCuttingRecipe != null;
    private bool DoneCutting => cuttingProgress >= activeCuttingRecipe.cutCount;
    private float CuttingProgressNormalized => (float)cuttingProgress / activeCuttingRecipe.cutCount;

    // --- public surface for visuals / camera / indicator ---
    public bool IsCutting => Cutting;
    public bool HasCuttableItem => HoldingObject && GetCuttingRecipe(currentKitchenObject.GetKitchenObjectSO()) != null;
    public bool PerfectSliceEnabled => perfectSliceEnabled;
    public float RhythmPhase => rhythmPhase;
    public float PerfectTargetCenter => perfectTargetCenter;
    public float PerfectWindow => perfectWindow;
    public Transform GetFocusPoint() => focusPoint != null ? focusPoint : spawnPoint;

    private void Start()
    {
        // each time the counter changes item, clear the cutting progress
        OnCounterItemChange += (_, _) => ResetCutting();

        // chain the onCut event with the onProgressChanged event and onAnyCut
        OnCut += (obj, arg) => OnProgressChanged.Invoke(obj, arg);
        OnCut += (obj, _) => OnAnyCut?.Invoke(obj, EventArgs.Empty);
    }

    private void Update()
    {
        if (Cutting && perfectSliceEnabled)
        {
            rhythmPhase = Mathf.Repeat(rhythmPhase + rhythmSpeed * Time.deltaTime, 1f);
        }
    }

    protected override void InteractAltAction(Player player)
    {
        if (Cutting)
        {
            Cut();
        }
        else if (HoldingObject)
        {
            // start cutting the current object if a recipe exists for it
            CuttingRecipeSO recipe = GetCuttingRecipe(currentKitchenObject.GetKitchenObjectSO());
            if (recipe)
            {
                InitalizeCutting(recipe);
                Cut();
            }
            else
            {
                Debug.Log(currentKitchenObject.GetKitchenObjectSO().objectName + " cannot be cut");
            }
        }
    }

    protected override bool FilterAllowedObject(KitchenObject kitchenObject)
    {
        return kitchenObject.GetKitchenObjectSO().objectName != "Plate";
    }

    #region Recipe Actions

    private CuttingRecipeSO GetCuttingRecipe(KitchenObjectSO from)
    {
        foreach (CuttingRecipeSO recipe in cuttingRecipes)
        {
            if (recipe.from == from)
            {
                return recipe;
            }
        }
        return null;
    }

    #endregion

    #region Cutting Actions

    private void InitalizeCutting(CuttingRecipeSO recipe)
    {
        if (Cutting)
        {
            Debug.LogError("Cannot have multiple active cutting recipe");
        }
        else
        {
            activeCuttingRecipe = recipe;
            rhythmPhase = 0f;
            SetProgress(0, false);
        }
    }

    private void ResetCutting()
    {
        activeCuttingRecipe = null;
        SetProgress(0, false);
    }

    private void Cut()
    {
        if (!Cutting)
        {
            Debug.LogError("Cannot cut when with no active cutting recipe");
            return;
        }

        bool perfect = perfectSliceEnabled && IsInPerfectWindow();

        SetProgress(cuttingProgress + 1, perfect);

        if (perfect)
        {
            OnAnyPerfectSlice?.Invoke(this, EventArgs.Empty);
        }

        // Checks for done cutting
        if (DoneCutting)
        {
            SpawnByproduct(activeCuttingRecipe);
            currentKitchenObject.DestroySelf();
            KitchenObject.Spawn(activeCuttingRecipe.to, this);
            ResetCutting();
        }
    }

    private bool IsInPerfectWindow()
    {
        // circular distance between the sweep phase and the target centre
        float distance = Mathf.Abs(Mathf.DeltaAngle(rhythmPhase * 360f, perfectTargetCenter * 360f)) / 360f;
        return distance <= perfectWindow;
    }

    private void SpawnByproduct(CuttingRecipeSO recipe)
    {
        if (recipe.byproduct == null || byproductOutput == null)
        {
            return;
        }
        if (byproductOutput.HoldingObject)
        {
            // tray already full; skip silently so the player isn't blocked
            return;
        }
        KitchenObject.Spawn(recipe.byproduct, byproductOutput);
    }

    private void SetProgress(int newProgress, bool perfect)
    {
        cuttingProgress = newProgress;
        OnCut?.Invoke(this, new CutProgressUpdatedArg
        {
            active = cuttingProgress != 0,
            perfect = perfect,
            progressNormalized = Cutting ? CuttingProgressNormalized : 0f,
            verb = Cutting ? activeCuttingRecipe.verb : CuttingRecipeSO.ProcessVerb.Cut,
        });
    }

    #endregion
}
