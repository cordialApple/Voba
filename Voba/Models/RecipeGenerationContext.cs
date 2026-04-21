using System.Collections.Generic;

namespace Voba.Models
{
    // Data model for the completed recipe ready for the MAUI front-end to display
    public class FullRecipe
    {
        public string Title { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
    }

    public class RecipeGenerationContext
    {
        public int ServingSize { get; set; }
        public decimal TargetBudget { get; set; }
        public List<string> DietaryRestrictions { get; set; } = new();

        // Free-text cuisine preference entered by the user (e.g. "Italian", "Thai").
        // Null or empty means no cuisine preference — Gemma may suggest any style.
        public string? CuisinePreference { get; set; }

        public List<RecipeOption> ProposedOptions { get; set; } = new();

        public RecipeOption? SelectedOption { get; set; }

        public FullRecipe? FinalRecipe { get; set; }

        public bool IsHandled { get; set; } = false;

        // Added to support your UI debugging
        public string DebugPrompt { get; set; } = string.Empty;
    }
}