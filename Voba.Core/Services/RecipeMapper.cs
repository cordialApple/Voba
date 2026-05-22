using System;
using System.Collections.Generic;
using System.Linq;
using Voba.Models;

namespace Voba.Services
{
    // Bridges the AI pipeline output (RecipeOption + FullRecipe) to the MongoDB
    // persistence model (Recipe). Without this there is no path from a generated
    // recipe to the database.
    public static class RecipeMapper
    {
        public static Recipe ToRecipe(RecipeGenerationContext context, string userId)
        {
            var option = context.SelectedOption
                ?? throw new InvalidOperationException("No recipe selected to save.");

            // Gemma yields ingredient names only; amount/unit are unknown here.
            var ingredients = option.Ingredients
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new Ingredient(name, 0m, string.Empty))
                .ToList();

            decimal cost = option.TotalCost > 0 ? option.TotalCost : option.EstimatedCost;

            string title = !string.IsNullOrWhiteSpace(context.FinalRecipe?.Title)
                ? context.FinalRecipe!.Title
                : option.Name;

            string instructions = context.FinalRecipe?.Instructions ?? string.Empty;

            NutritionInfo? nutrition = context.FinalRecipe?.Nutrition ?? option.Nutrition;

            return new Recipe(userId, title, ingredients, cost, instructions, nutrition);
        }
    }
}
