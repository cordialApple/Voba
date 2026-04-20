using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class GroceryService
    {
        private readonly ISpoonacularService _spoonacular;
        private readonly SpoonacularAdapter _adapter;
        private readonly IGroceryListRepository _groceryRepository;
        private readonly IPriceStrategy _priceStrategy;

        public GroceryService(
            ISpoonacularService spoonacular,
            SpoonacularAdapter adapter,
            IGroceryListRepository groceryRepository,
            IPriceStrategy priceStrategy)
        {
            _spoonacular       = spoonacular;
            _adapter           = adapter;
            _groceryRepository = groceryRepository;
            _priceStrategy     = priceStrategy;
        }

        /// <summary>Builds and saves a budget-optimized grocery list from a Spoonacular recipe.</summary>
        public async Task<ServiceResult<GroceryList>> CreateFromRecipeAsync(
            int recipeId, string userId, decimal budget)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return ServiceResult<GroceryList>.Fail(
                    ErrorCodes.ValidationError, "User ID is required.");

            if (budget <= 0)
                return ServiceResult<GroceryList>.Fail(
                    ErrorCodes.ValidationError, "Budget must be positive.");

            var detail = await _spoonacular.GetRecipeByIdAsync(recipeId);
            if (detail is null)
                return ServiceResult<GroceryList>.Fail(
                    ErrorCodes.NotFound, "Recipe not found.");

            var ingredients = _adapter.AdaptIngredients(detail);
            var optimized   = _priceStrategy.Optimize(ingredients, budget);

            var groceryList = new GroceryListBuilder()
                .ForUser(userId)
                .WithBudget(budget)
                .WithItems(optimized)
                .WithEstimatedCost(optimized.Sum(i => i.EstimatedCost))
                .Build();

            await _groceryRepository.SaveAsync(groceryList);
            return ServiceResult<GroceryList>.Ok(groceryList);
        }
    }
}
