using UnityEngine;

/// <summary>
/// Logs the step-by-step procedure for each incoming order to the Console, so you
/// can see exactly how to make the dish (take → cut → cook → plate). Assign the
/// generated <see cref="RecipeDatabaseSO"/>. This is the same data a cookbook UI
/// will use later.
/// </summary>
public class OrderProcedureLogger : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseSO recipeDatabase;
    [SerializeField] private bool logOnOrderPlaced = true;

    private void Start()
    {
        if (logOnOrderPlaced && DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnOrderPlaced += DeliveryManager_OnOrderPlaced;
        }
    }

    private void OnDestroy()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnOrderPlaced -= DeliveryManager_OnOrderPlaced;
        }
    }

    private void DeliveryManager_OnOrderPlaced(object sender, MenuRecipeSO dish)
    {
        if (recipeDatabase == null)
        {
            Debug.LogWarning("[OrderProcedureLogger] No RecipeDatabase assigned.");
            return;
        }

        var steps = Cookbook.GetProcedure(recipeDatabase, dish);
        Debug.Log(Cookbook.Format(dish, steps));
    }
}
