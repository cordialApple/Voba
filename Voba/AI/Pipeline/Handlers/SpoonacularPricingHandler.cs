using System;
using System.Threading.Tasks;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;

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
                await RecipeGenerationPolicy.EnrichAsync(recipe, context.ServingSize, _enrichment);

            await base.HandleAsync(context);
        }
    }
}
