using System;
using System.Threading.Tasks;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.AI.Pipeline.Handlers
{
    /// <summary>
    /// CHAIN LINK — Enrichment step.
    ///
    /// Sits between GemmaIdeationHandler and (after user selection) GemmaFullRecipeHandler:
    ///   GemmaIdeationHandler > SpoonacularPricingHandler
    ///
    /// For each proposed recipe it asks the configured IRecipeEnrichmentService
    /// (real Spoonacular, or the offline fake) for:
    ///   EstimatedCost — per-serving cost in USD
    ///   TotalCost     — cost across all servings in USD
    ///   Nutrition     — per-serving calories + macros
    ///
    /// If the provider returns nothing usable, Gemma's original EstimatedCost is
    /// preserved and TotalCost is derived from it.
    /// </summary>
    public class SpoonacularPricingHandler : RecipePipelineHandler
    {
        private readonly IRecipeEnrichmentService _enrichment;

        public SpoonacularPricingHandler(IRecipeEnrichmentService enrichment)
        {
            _enrichment = enrichment;
        }

        public override async Task HandleAsync(RecipeGenerationContext context)
        {
            foreach (var recipe in context.ProposedOptions)
                await EnrichSingleRecipeAsync(recipe, context.ServingSize);

            await base.HandleAsync(context);
        }

        private async Task EnrichSingleRecipeAsync(RecipeOption recipe, int servingSize)
        {
            if (servingSize <= 0) servingSize = 1;

            var enrichment = await _enrichment.EnrichAsync(recipe.Ingredients, servingSize);

            if (enrichment != null)
            {
                if (enrichment.TotalCostUsd > 0)
                {
                    recipe.EstimatedCost = enrichment.CostPerServingUsd;
                    recipe.TotalCost = enrichment.TotalCostUsd;
                }

                if (enrichment.Nutrition.HasData)
                    recipe.Nutrition = enrichment.Nutrition;
            }

            // Fallback — provider gave no usable cost. Keep Gemma's EstimatedCost
            // and derive TotalCost from it.
            if (recipe.TotalCost == 0 && recipe.EstimatedCost > 0)
                recipe.TotalCost = Math.Round(recipe.EstimatedCost * servingSize, 2);
        }
    }
}
