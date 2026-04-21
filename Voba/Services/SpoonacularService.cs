using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using spoonacular.api;
using spoonacular.Api;
using spoonacular.Client;
using spoonacular.Models;

namespace Voba.Spoonacular
{
    public class SpoonacularService
    {
        private readonly IngredientsApi _ingredientsApi;
        private readonly RecipesApi _recipesApi;

        public SpoonacularService()
        {
            var config = new Configuration
            {
                BasePath = "https://api.spoonacular.com"
            };

            config.ApiKey["x-api-key"] = ApiSettings.SpoonacularApiKey;

            _ingredientsApi = new IngredientsApi(config);
            _recipesApi = new RecipesApi(config);
        }

        public async Task<IngredientSearch200Response?> IngredientSearchAsync(
            string query,
            int number = 1)
        {
            try
            {
                return await _ingredientsApi.IngredientSearchAsync(query, number: number);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] IngredientSearch error: {ex.Message}");
                return null;
            }
        }

        public async Task<IngredientInformation?> GetIngredientInformationAsync(
            int ingredientId,
            decimal? amount = 1,
            string? unit = null)
        {
            try
            {
                return await _ingredientsApi.GetIngredientInformationAsync(
                    ingredientId,
                    amount: amount,
                    unit: unit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] GetIngredientInformation error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<IngredientInformation>> ParseIngredientsAsync(
            string ingredientList,
            decimal servings)
        {
            try
            {
                var result = await _recipesApi.ParseIngredientsAsync(
                    ingredientList,
                    servings,
                    includeNutrition: false);

                return result ?? new List<IngredientInformation>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] ParseIngredients error: {ex.Message}");
                return new List<IngredientInformation>();
            }
        }

        public async Task<decimal?> GetPriceInUsdAsync(
            string ingredientName,
            decimal amount = 1,
            string? unit = null)
        {
            if (string.IsNullOrWhiteSpace(ingredientName))
                return null;

            var searchResult = await IngredientSearchAsync(ingredientName.Trim(), number: 1);

            if (searchResult?.Results == null || searchResult.Results.Count == 0)
                return null;

            var info = await GetIngredientInformationAsync(
                searchResult.Results[0].Id,
                amount: amount,
                unit: unit);

            if (info?.EstimatedCost == null)
                return null;

            return Math.Round(info.EstimatedCost.Value / 100m, 2);
        }
    }
}
