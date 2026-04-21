using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using spoonacular.Api;
using spoonacular.Client;
using spoonacular.Models;

namespace Voba.Spoonacular
{
    /// <summary>
    /// Wraps the spoonacular SDK's IngredientsApi and RecipesApi with the
    /// operations Voba needs for ingredient search and accurate pricing.
    ///
    /// Registered as a singleton in MauiProgram so the SDK configuration
    /// and HttpClient are created once for the lifetime of the app.
    /// </summary>
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

        // ── 1. Search ────────────────────────────────────────────────────────

        /// <summary>
        /// Searches Spoonacular for ingredients whose name matches the query.
        /// Returns null on any network or API error.
        /// </summary>
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

        // ── 2. Ingredient information (includes estimatedCost) ───────────────

        /// <summary>
        /// Fetches full ingredient information for a known Spoonacular id.
        /// EstimatedCost.Value is in US Cents — divide by 100 to get USD.
        /// Returns null on any network or API error.
        /// </summary>
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

        // ── 3. Parse entire ingredient list in ONE call ──────────────────────

        /// <summary>
        /// Sends the full ingredient list for a recipe to Spoonacular's
        /// ParseIngredients endpoint and gets back a priced IngredientInformation
        /// object for each ingredient — all in a single API call.
        ///
        /// ingredientList — newline-separated list exactly as Gemma produced it
        ///                  e.g. "2 cups chicken broth\n1 tbsp olive oil\n..."
        /// servings       — passed to Spoonacular so costs are scaled correctly
        ///
        /// Returns an empty list on any network or API error so the caller
        /// can fall back to Gemma's original estimate without crashing.
        /// </summary>
        public async Task<List<IngredientInformation>> ParseIngredientsAsync(
            string ingredientList,
            decimal servings)
        {
            try
            {
                var result = await _recipesApi.ParseIngredientsAsync(
                    ingredientList,
                    servings,
                    includeNutrition: false);   // false keeps the response small — we only need cost

                return result ?? new List<IngredientInformation>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] ParseIngredients error: {ex.Message}");
                return new List<IngredientInformation>();
            }
        }

        // ── 4. Legacy convenience: single ingredient name → USD price ────────

        /// <summary>
        /// Kept for any future single-ingredient lookups (e.g. the ingredient
        /// search feature on the front-end).
        /// Not used by SpoonacularPricingHandler — that now uses ParseIngredientsAsync.
        /// </summary>
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