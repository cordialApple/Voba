using System.Runtime.CompilerServices;

// Lets the test project reach the internal cost/nutrition reduction
// (SpoonacularEnrichmentService.ComputeEnrichment and its ParsedIngredient
// projection) without exposing them to the rest of the app.
[assembly: InternalsVisibleTo("Voba.Core.Tests")]
