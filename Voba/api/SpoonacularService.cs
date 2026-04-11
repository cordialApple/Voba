using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using spoonacular.Api;
using spoonacular.Client;
using spoonacular.Model;

namespace Voba.api
{
    public class SpoonacularService
    {
        private readonly RecipesApi _recipesApi;

        public SpoonacularService()
        {
            Configuration config = new();
            config.BasePath = "https://api.spoonacular.com";

            // Pulls your secret key from ApiSettings.cs
            config.ApiKey.Add("x-api-key", ApiSettings.SpoonacularApiKey);

            _recipesApi = new RecipesApi(config);
        }

        // 1. Search for recipes by name
        public async Task<SearchRecipes200Response?> SearchRecipes(string query, int number = 5)
        {
            try
            {
                return await _recipesApi.SearchRecipesAsync(query, number: number);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search Error: {ex.Message}");
                return null;
            }
        }

        // 2. Get the ingredients for a recipe by ID
        public async Task<GetRecipeIngredientsByID200Response?> GetRecipeIngredients(int recipeId)
        {
            try
            {
                return await _recipesApi.GetRecipeIngredientsByIDAsync(recipeId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ingredients Error: {ex.Message}");
                return null;
            }
        }

        // 3. Get the price breakdown for a recipe by ID
        public async Task<GetRecipePriceBreakdownByID200Response?> GetRecipePrice(int recipeId)
        {
            try
            {
                return await _recipesApi.GetRecipePriceBreakdownByIDAsync(recipeId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Price Error: {ex.Message}");
                return null;
            }
        }
    }
}
