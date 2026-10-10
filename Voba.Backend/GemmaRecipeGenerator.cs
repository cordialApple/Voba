using Microsoft.SemanticKernel;
using Voba.AI.Pipeline.Handlers;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Backend;

public sealed class GemmaRecipeGenerator(Kernel kernel, IRecipeEnrichmentService enrichment)
    : IRecipeGenerator
{
    public async Task GenerateOptionsAsync(RecipeGenerationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ideation = new GemmaIdeationHandler(kernel);
        ideation.SetNext(new SpoonacularPricingHandler(enrichment));
        await ideation.HandleAsync(context);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task GenerateFullAsync(RecipeGenerationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await new GemmaFullRecipeHandler(kernel).HandleAsync(context);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
