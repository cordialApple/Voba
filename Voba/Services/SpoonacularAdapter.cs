using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class SpoonacularAdapter
    {
        /// <summary>Converts Spoonacular recipe detail into a list of domain Ingredient models.</summary>
        public List<Ingredient> AdaptIngredients(SpoonacularRecipeDetail detail)
        {
            return detail.Ingredients
                .Select(i => new Ingredient(i.Name, i.Amount, i.Unit))
                .ToList();
        }

        /// <summary>Builds a GroceryList from Spoonacular recipe detail via the Builder pattern.</summary>
        public GroceryList AdaptToGroceryList(
            SpoonacularRecipeDetail detail, string userId, decimal budget)
        {
            return new GroceryListBuilder()
                .ForUser(userId)
                .WithBudget(budget)
                .WithItems(detail.Ingredients.Select(i =>
                    new Ingredient(i.Name, i.Amount, i.Unit)))
                .WithEstimatedCost(detail.TotalCost)
                .Build();
        }
    }
}
