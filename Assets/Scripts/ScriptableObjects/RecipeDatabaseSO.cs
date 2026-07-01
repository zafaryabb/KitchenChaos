using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat list of every cutting + frying recipe and every dish, so systems can
/// reason about "how is this item made" at runtime (the <see cref="Cookbook"/>
/// traces these chains). Populated automatically by the FishContentGenerator.
/// </summary>
[CreateAssetMenu(menuName = "Tide & Table/Recipe Database")]
public class RecipeDatabaseSO : ScriptableObject
{
    public List<CuttingRecipeSO> cuttingRecipes = new();
    public List<FryingRecipeSO> fryingRecipes = new();
    public List<MenuRecipeSO> dishes = new();
}
