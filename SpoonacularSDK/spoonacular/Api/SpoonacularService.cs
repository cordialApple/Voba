using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using spoonacular.Api;
using spoonacular.Client;
using spoonacular.Models;
using Voba;

namespace spoonacular.api
{
    public class SpoonacularService
    {
        private readonly RecipesApi _recipesApi;
        private readonly IngredientsApi _ingredientsApi;

        public SpoonacularService()
        {
            Configuration config = new();
            config.BasePath = "https://api.spoonacular.com";

            // Pulls your secret key from ApiSettings.cs
            config.ApiKey.Add("x-api-key", ApiSettings.SpoonacularApiKey);

            _recipesApi = new RecipesApi(config);
            _ingredientsApi = new IngredientsApi(config);
        }  



        // 4. Search for an ingredient by name and return candidate ingredient IDs
        public async Task<IngredientSearch200Response?> IngredientSearch(string query, int number = 1)
        {
            try
            {
                return await _ingredientsApi.IngredientSearchAsync(query, number: number);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ingredient Search Error: {ex.Message}");
                return null;
            }
        }

        // 5. Get ingredient details and estimated cost from an ingredient ID
        public async Task<IngredientInformation?> GetIngredientInformation(int ingredientId, decimal? amount = 1, string? unit = null)
        {
            try
            {
                return await _ingredientsApi.GetIngredientInformationAsync(ingredientId, amount: amount, unit: unit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ingredient Information Error: {ex.Message}");
                return null;
            }
        }

    }
}
