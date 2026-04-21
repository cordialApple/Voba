using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class SpoonacularAdapter
    {
        public List<Ingredient> AdaptIngredients(SpoonacularRecipeDetail detail)
        {
            return detail.Ingredients
                .Select(i => new Ingredient(i.Name, i.Amount, i.Unit)
                {
                    EstimatedCost = i.EstimatedCost
                })
                .ToList();
        }

        public GroceryList AdaptToGroceryList(
            SpoonacularRecipeDetail detail, string userId, decimal budget)
        {
            return new GroceryListBuilder()
                .ForUser(userId)
                .WithBudget(budget)
                .WithItems(detail.Ingredients.Select(i =>
                    new Ingredient(i.Name, i.Amount, i.Unit)
                    {
                        EstimatedCost = i.EstimatedCost
                    }))
                .WithEstimatedCost(detail.TotalCost)
                .Build();
        }
    }
}
