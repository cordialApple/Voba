using Microsoft.SemanticKernel;
using Voba.AI.Pipeline.Handlers;
using Voba.Models;
using Voba.Services;

var model = Environment.GetEnvironmentVariable("VOBA_OLLAMA_MODEL") ?? "gemma3:4b";
var endpoint = new Uri(Environment.GetEnvironmentVariable("VOBA_OLLAMA_ENDPOINT")
    ?? "http://localhost:11434");
var kernel = Kernel.CreateBuilder().AddOllamaChatCompletion(model, endpoint).Build();
var mongoUri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
if (!string.IsNullOrWhiteSpace(mongoUri))
{
    await LiveSmoke.RunAsync(kernel, mongoUri, model);
    return;
}

var ideation = new GemmaIdeationHandler(kernel);
ideation.SetNext(new SpoonacularPricingHandler(new FakeEnrichmentService()));
var context = new RecipeGenerationContext
{
    ServingSize = 2,
    TargetBudget = 100m,
    DietaryRestrictions = ["vegan"]
};

await ideation.HandleAsync(context);
context.ProposedOptions = RecipeGenerationPolicy.WithinBudget(
    context.ProposedOptions, context.TargetBudget, context.ServingSize);
if (context.ProposedOptions.Count == 0)
    throw new InvalidOperationException("Gemma returned no usable recipe options.");
if (context.ProposedOptions.Any(option => option.DataSource != RecipeDataSource.Synthetic))
    throw new InvalidOperationException("Demo enrichment source missing.");
if (context.ProposedOptions.Any(option => !DietaryCompliancePolicy.AllowsOption(
        option, context.DietaryRestrictions)))
    throw new InvalidOperationException("Gemma returned a non-vegan option.");

context.SelectedOption = context.ProposedOptions[0];
context.IsHandled = false;
await new GemmaFullRecipeHandler(kernel).HandleAsync(context);
if (string.IsNullOrWhiteSpace(context.FinalRecipe?.Instructions))
    throw new InvalidOperationException("Gemma returned no cooking instructions.");
if (!DietaryCompliancePolicy.AllowsFullRecipe(context.SelectedOption,
        context.FinalRecipe.Instructions, context.DietaryRestrictions))
    throw new InvalidOperationException("Gemma returned non-vegan instructions.");

Console.WriteLine($"Options: {context.ProposedOptions.Count}; selected source: {context.SelectedOption.DataSource}; instructions: present");
