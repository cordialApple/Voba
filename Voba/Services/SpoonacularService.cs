using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using spoonacular.Api;
using spoonacular.Client;
using spoonacular.Models;

namespace Voba.Spoonacular
{
    /// <summary>
    /// Wrapper service for the Spoonacular API, handling ingredient resolution and pricing.
    /// Designed to be registered as a singleton to reuse HTTP connections and SDK configurations.
    /// </summary>
    public class SpoonacularService
    {
        private readonly IngredientsApi _ingredientsApi;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        /// <summary>
        /// Initializes the Spoonacular SDK configuration and a fallback HttpClient for unsupported endpoints.
        /// </summary>
        public SpoonacularService()
        {
            // Retrieve API key from static configuration.
            _apiKey = ApiSettings.SpoonacularApiKey;

            // Initialize SDK configuration object.
            var config = new Configuration
            {
                BasePath = "https://api.spoonacular.com"
            };

            // Append authorization header for SDK-managed requests.
            config.ApiKey["x-api-key"] = _apiKey;

            // Instantiate primary SDK client.
            _ingredientsApi = new IngredientsApi(config);

            // Instantiate secondary HTTP client for direct endpoint access.
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.spoonacular.com")
            };
        }

        // ── 1. Search ────────────────────────────────────────────────────────

        /// <summary>
        /// Executes an ingredient search query via the Spoonacular SDK.
        /// </summary>
        /// <param name="query">The ingredient name to search for.</param>
        /// <param name="number">Maximum number of results to return. Default is 1.</param>
        /// <returns>IngredientSearch200Response on success; null on failure.</returns>
        public async Task<IngredientSearch200Response?> IngredientSearchAsync(
            string query,
            int number = 1)
        {
            try
            {
                // Execute asynchronous search request.
                return await _ingredientsApi.IngredientSearchAsync(query, number: number);
            }
            catch (Exception ex)
            {
                // Log exception and return null to prevent application crash on network failure.
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] IngredientSearch error: {ex.Message}");
                return null;
            }
        }

        // ── 2. Ingredient Information ────────────────────────────────────────

        /// <summary>
        /// Retrieves detailed ingredient information by Spoonacular ID.
        /// </summary>
        /// <param name="ingredientId">The Spoonacular integer ID.</param>
        /// <param name="amount">Quantity of the ingredient.</param>
        /// <param name="unit">Unit of measurement.</param>
        /// <returns>IngredientInformation on success; null on failure. EstimatedCost.Value is in US Cents.</returns>
        public async Task<IngredientInformation?> GetIngredientInformationAsync(
            int ingredientId,
            decimal? amount = 1,
            string? unit = "gram")
        {
            try
            {
                // Execute asynchronous request for specific ingredient metadata.
                return await _ingredientsApi.GetIngredientInformationAsync(
                    ingredientId,
                    amount: amount,
                    unit: unit);
            }
            catch (Exception ex)
            {
                // Log exception and return null.
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] GetIngredientInformation error: {ex.Message}");
                return null;
            }
        }

        // ── 3. Single Price Lookup ───────────────────────────────────────────

        /// <summary>
        /// Resolves a single ingredient name to a USD price via sequential Search and Info API calls.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="GetBatchPricesAsync"/> for multiple ingredients to optimize API quota usage.
        /// </remarks>
        /// <returns>Decimal USD price on success; null on resolution failure.</returns>
        public async Task<decimal?> GetPriceInUsdAsync(
            string ingredientName,
            decimal amount = 1,
            string? unit = "gram")
        {
            // Validate input to prevent unnecessary API invocations.
            if (string.IsNullOrWhiteSpace(ingredientName))
                return null;

            // Retrieve matching ingredient ID.
            var searchResult = await IngredientSearchAsync(ingredientName.Trim(), number: 1);

            // Terminate if no matches exist in the Spoonacular database.
            if (searchResult?.Results == null || searchResult.Results.Count == 0)
                return null;

            // Extract ID from the primary result.
            int id = searchResult.Results[0].Id;

            // Retrieve pricing metadata using the resolved ID.
            var info = await GetIngredientInformationAsync(id, amount: amount, unit: unit);

            // Terminate if pricing data is unavailable.
            if (info?.EstimatedCost == null)
                return null;

            // Convert integer cents to decimal USD and round to nearest cent.
            return Math.Round((decimal)info.EstimatedCost.Value / 100m, 2);
        }

        // ── 4. Batch Price Lookup ────────────────────────────────────────────

        /// <summary>
        /// Executes a batch pricing request via POST /recipes/parseIngredients.
        /// Costs 1 API quota point per invocation regardless of collection size.
        /// </summary>
        /// <param name="rawIngredients">Collection of unparsed ingredient strings.</param>
        /// <returns>A dictionary mapping the original raw ingredient string to its parsed USD price.</returns>
        public async Task<Dictionary<string, decimal>> GetBatchPricesAsync(
            IReadOnlyList<string> rawIngredients)
        {
            // Initialize dictionary with case-insensitive key comparison.
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            // Return empty dictionary if input collection is null or empty.
            if (rawIngredients == null || rawIngredients.Count == 0)
                return result;

            // Format input collection into a newline-delimited string required by the API.
            // Parenthetical modifiers are removed prior to joining to improve parsing accuracy.
            string ingredientList = string.Join("\n", rawIngredients.Select(StripParentheticals));

            // Construct target URI with authorization parameter.
            string url = $"/recipes/parseIngredients?apiKey={_apiKey}";

            // Construct form-urlencoded payload.
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("ingredientList", ingredientList),
                new KeyValuePair<string, string>("servings",        "1"),
                new KeyValuePair<string, string>("includeNutrition","false"),
            });

            string json;
            try
            {
                // Execute POST request.
                var response = await _httpClient.PostAsync(url, formData);

                // Throw exception if HTTP status code indicates failure (e.g., 400, 500).
                response.EnsureSuccessStatusCode();

                // Extract response body as string.
                json = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                // Log exception and return empty dictionary to allow fallback processing.
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] parseIngredients HTTP error: {ex.Message}");
                return result;
            }

            try
            {
                // Parse JSON response into a DOM structure.
                using var doc = JsonDocument.Parse(json);

                // Extract the root array elements.
                var elements = doc.RootElement.EnumerateArray().ToList();

                // Iterate through returned elements, mapping them back to the original index.
                for (int i = 0; i < elements.Count && i < rawIngredients.Count; i++)
                {
                    var element = elements[i];

                    // Verify existence of estimatedCost property.
                    if (!element.TryGetProperty("estimatedCost", out var costObj))
                        continue;

                    // Verify existence of nested value property.
                    if (!costObj.TryGetProperty("value", out var valueEl))
                        continue;

                    // Extract numerical cost value.
                    double cents = valueEl.GetDouble();

                    // Discard zero or negative valuations.
                    if (cents <= 0)
                        continue;

                    // Convert cents to USD and populate the result dictionary using the original string key.
                    decimal usd = Math.Round((decimal)cents / 100m, 2);
                    result[rawIngredients[i]] = usd;
                }
            }
            catch (JsonException ex)
            {
                // Log parsing failures.
                System.Diagnostics.Debug.WriteLine($"[Spoonacular] parseIngredients JSON parse error: {ex.Message}");
            }

            return result;
        }

        // ── Internal Helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Strips leading numeric quantities and unit string tokens from an ingredient payload.
        /// </summary>
        internal static string StripQuantity(string ingredient)
        {
            // Validate input.
            if (string.IsNullOrWhiteSpace(ingredient))
                return ingredient;

            // Define known measurement unit tokens.
            var units = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cup", "cups", "tbsp", "tsp", "tablespoon", "tablespoons",
                "teaspoon", "teaspoons", "oz", "ounce", "ounces",
                "lb", "lbs", "pound", "pounds", "g", "gram", "grams",
                "kg", "kilogram", "kilograms", "ml", "l", "liter", "liters",
                "litre", "litres", "handful", "pinch", "dash", "slice",
                "slices", "piece", "pieces", "clove", "cloves",
                "small", "medium", "large", "whole", "fresh", "dried",
                "can", "cans", "bunch", "bunches", "sprig", "sprigs",
                "fillet", "fillets", "stalk", "stalks", "head", "heads",
                "package", "packages", "bag", "bags",
            };

            // Tokenize the input string by whitespace.
            var tokens = ingredient.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int start = 0;

            // Iterate through tokens, advancing the start index past quantities and units.
            foreach (var token in tokens)
            {
                if (IsNumericOrFraction(token) || units.Contains(token))
                {
                    start++;
                    continue;
                }
                break; // Break execution on first non-quantity token.
            }

            // Return the original string if fully consumed, otherwise return the joined remaining tokens.
            return start >= tokens.Length
                ? ingredient.Trim()
                : string.Join(" ", tokens[start..]);
        }

        /// <summary>
        /// Evaluates if a string token is a valid decimal or fractional value.
        /// </summary>
        internal static bool IsNumericOrFraction(string token)
        {
            // Evaluate as standard decimal.
            if (decimal.TryParse(token, out _)) return true;

            // Split by forward slash for fraction evaluation.
            var parts = token.Split('/');

            // Validate fraction contains exactly two parts, both of which are valid decimals.
            return parts.Length == 2
                   && decimal.TryParse(parts[0], out _)
                   && decimal.TryParse(parts[1], out _);
        }

        /// <summary>
        /// Truncates the ingredient string at the first occurrence of an open parenthesis.
        /// Required for Spoonacular API pricing compatibility.
        /// </summary>
        private static string StripParentheticals(string ingredient)
        {
            // Locate index of open parenthesis.
            int paren = ingredient.IndexOf('(');

            // Return truncated substring if parenthesis exists, otherwise return original string.
            return paren > 0
                ? ingredient[..paren].Trim()
                : ingredient;
        }
    }
}