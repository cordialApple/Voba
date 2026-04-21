using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voba.Models;
using Voba.Spoonacular;

namespace Voba.AI.Pipeline.Handlers
{
    /// <summary>
    /// CHAIN LINK — Pricing Step.
    ///
    /// Sits between GemmaIdeationHandler and GemmaFullRecipeHandler:
    ///   GemmaIdeationHandler → SpoonacularPricingHandler → GemmaFullRecipeHandler
    ///
    /// BEFORE: 1 search call + 1 info call per ingredient = ~16 calls per Generate.
    /// AFTER:  1 ParseIngredients call per recipe         =  2 calls per Generate.
    ///
    /// Sets two cost properties on each RecipeOption:
    ///   EstimatedCost — Spoonacular-priced cost for 1 serving
    ///   TotalCost     — EstimatedCost × context.ServingSize
    ///
    /// If ParseIngredients returns nothing, Gemma's original EstimatedCost is
    /// preserved and TotalCost is still calculated from it.
    /// </summary>
    public class SpoonacularPricingHandler : RecipePipelineHandler
    {
        private readonly SpoonacularService _spoonacular;

        public SpoonacularPricingHandler(SpoonacularService spoonacular)
        {
            _spoonacular = spoonacular;
        }

        public override async Task HandleAsync(RecipeGenerationContext context)
        {
            if (context.ProposedOptions.Count == 0)
            {
                await base.HandleAsync(context);
                return;
            }

            foreach (var recipe in context.ProposedOptions)
            {
                await RepriceSingleRecipeAsync(recipe, context.ServingSize);
            }

            await base.HandleAsync(context);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private async Task RepriceSingleRecipeAsync(RecipeOption recipe, int servingSize)
        {
            // Build the newline-separated ingredient list ParseIngredients expects.
            // One ingredient per line, exactly as Gemma produced it.
            string ingredientList = string.Join("\n", recipe.Ingredients);

            // ONE Spoonacular call for the entire ingredient list.
            var parsed = await _spoonacular.ParseIngredientsAsync(
                ingredientList,
                servings: servingSize);

            if (parsed.Count > 0)
            {
                // Sum up EstimatedCost.Value across all returned ingredients.
                // EstimatedCost.Value is always in US Cents — convert to USD.
                decimal totalCents = parsed
                    .Where(i => i.EstimatedCost != null)
                    .Sum(i => i.EstimatedCost!.Value);

                if (totalCents > 0)
                {
                    // The total from ParseIngredients is already scaled to servingSize
                    // because we passed servings to the endpoint.
                    // Divide by servingSize to get the per-serving cost.
                    decimal totalUsd = Math.Round(totalCents / 100m, 2);
                    decimal perServingUsd = Math.Round(totalUsd / servingSize, 2);

                    recipe.EstimatedCost = perServingUsd;
                    recipe.TotalCost = totalUsd;
                    return;
                }
            }

            // Fallback — Spoonacular returned nothing usable.
            // Keep Gemma's original EstimatedCost and derive TotalCost from it.
            recipe.TotalCost = Math.Round(recipe.EstimatedCost * servingSize, 2);
        }
    }
}