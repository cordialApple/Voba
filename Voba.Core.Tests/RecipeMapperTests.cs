using System;
using System.Collections.Generic;
using System.Linq;
using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests
{
    // RecipeMapper is the only bridge from the AI pipeline output to the persisted
    // Recipe. These tests pin the field selection rules (cost source, title/nutrition
    // precedence, ingredient cleaning) that the save path depends on.
    public class RecipeMapperTests
    {
        [Fact]
        public void Throws_when_no_option_is_selected()
        {
            var context = new RecipeGenerationContext();

            Assert.Throws<InvalidOperationException>(
                () => RecipeMapper.ToRecipe(context, "user-1"));
        }

        [Fact]
        public void Maps_selected_option_and_cleans_ingredients()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption
                {
                    Name = "Bean Chili",
                    Ingredients = new List<string> { "beans", "  ", "tomato", "" },
                    EstimatedCost = 4.00m,
                    TotalCost = 7.50m,
                    Nutrition = new NutritionInfo { Calories = 500m },
                },
            };

            var recipe = RecipeMapper.ToRecipe(context, "user-1");

            Assert.Equal("user-1", recipe.UserId);
            Assert.Equal("Bean Chili", recipe.Title);
            Assert.Equal(new[] { "beans", "tomato" },
                recipe.Ingredients.Select(i => i.Name).ToArray());
            Assert.Equal(500m, recipe.Nutrition!.Calories);
            Assert.Equal(string.Empty, recipe.Instructions);
        }

        [Fact]
        public void Prefers_total_cost_when_positive()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption
                {
                    Name = "x",
                    EstimatedCost = 4.00m,
                    TotalCost = 7.50m,
                },
            };

            Assert.Equal(7.50m, RecipeMapper.ToRecipe(context, "u").EstimatedCost);
        }

        [Fact]
        public void Falls_back_to_estimated_cost_when_total_is_zero()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption
                {
                    Name = "x",
                    EstimatedCost = 3.25m,
                    TotalCost = 0m,
                },
            };

            Assert.Equal(3.25m, RecipeMapper.ToRecipe(context, "u").EstimatedCost);
        }

        [Fact]
        public void Final_recipe_title_instructions_and_nutrition_take_precedence()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption
                {
                    Name = "Option Name",
                    Nutrition = new NutritionInfo { Calories = 100m },
                },
                FinalRecipe = new FullRecipe
                {
                    Title = "Final Title",
                    Instructions = "Mix and cook.",
                    Nutrition = new NutritionInfo { Calories = 250m },
                },
            };

            var recipe = RecipeMapper.ToRecipe(context, "u");

            Assert.Equal("Final Title", recipe.Title);
            Assert.Equal("Mix and cook.", recipe.Instructions);
            Assert.Equal(250m, recipe.Nutrition!.Calories);
        }

        [Fact]
        public void Falls_back_to_option_name_when_final_title_is_blank()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption { Name = "Option Name" },
                FinalRecipe = new FullRecipe { Title = "   ", Instructions = "steps" },
            };

            var recipe = RecipeMapper.ToRecipe(context, "u");

            Assert.Equal("Option Name", recipe.Title);
            Assert.Equal("steps", recipe.Instructions);
        }
    }
}
