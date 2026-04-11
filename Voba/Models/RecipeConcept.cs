using System.Collections.Generic;

namespace Voba.Models
{
    public class RecipeConcept
    {
        // Initialized to empty to guarantee it's never null
        public string Name { get; set; } = string.Empty;

        // Initialized to an empty list
        public List<string> Ingredients { get; set; } = new();

        public decimal EstimatedCost { get; set; }
    }

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
        public List<RecipeConcept> ProposedOptions { get; set; } = new();

        // These use the '?' because they are legitimately null during Pipeline 1 
        // and only get populated during Pipeline 2.
        public RecipeConcept? SelectedOption { get; set; }
        public FullRecipe? FinalRecipe { get; set; }

        public bool IsHandled { get; set; } = false;
    }
}