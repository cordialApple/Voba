using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
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

        [Fact]
        public void Persists_selected_cost_source()
        {
            var context = new RecipeGenerationContext
            {
                DataSource = RecipeDataSource.Estimate,
                SelectedOption = new RecipeOption
                {
                    Name = "Bean stew",
                    DataSource = RecipeDataSource.Real,
                    NutritionSource = RecipeDataSource.Synthetic
                }
            };

            var recipe = RecipeMapper.ToRecipe(context, "507f1f77bcf86cd799439011");
            typeof(Recipe).GetProperty(nameof(Recipe.Id))!.SetValue(recipe, ObjectId.GenerateNewId().ToString());
            var restored = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Recipe>(recipe.ToBson());

            Assert.Equal(RecipeDataSource.Real, recipe.DataSource);
            Assert.Equal(RecipeDataSource.Real, restored.DataSource);
            Assert.Equal(RecipeDataSource.Synthetic, restored.NutritionSource);
        }

        [Fact]
        public void Generated_ingredients_serialize_inside_saved_recipe()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption
                {
                    Name = "Bean stew",
                    Ingredients = new List<string> { "beans", "tomato" }
                }
            };
            var recipe = RecipeMapper.ToRecipe(context, "507f1f77bcf86cd799439011");
            typeof(Recipe).GetProperty(nameof(Recipe.Id))!.SetValue(recipe, ObjectId.GenerateNewId().ToString());

            var restored = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Recipe>(recipe.ToBson());

            Assert.Equal(new[] { "beans", "tomato" },
                restored.Ingredients.Select(ingredient => ingredient.Name));
            Assert.All(restored.Ingredients, ingredient => Assert.True(ObjectId.TryParse(ingredient.Id, out _)));
            Assert.Equal(recipe.Ingredients.Select(ingredient => ingredient.Id),
                restored.Ingredients.Select(ingredient => ingredient.Id));
        }

        [Fact]
        public void Saves_generation_context_and_round_trips_through_bson()
        {
            var context = new RecipeGenerationContext
            {
                ServingSize = 3,
                TargetBudget = 24m,
                DietaryRestrictions = ["vegan", "no peanuts"],
                CuisinePreference = "Thai",
                SelectedOption = new RecipeOption { Name = "Tofu", Ingredients = ["tofu"] },
                FinalRecipe = new FullRecipe { Title = "Tofu", Instructions = "1. Cook." }
            };
            var recipe = RecipeMapper.ToRecipe(context, "507f1f77bcf86cd799439011");
            typeof(Recipe).GetProperty(nameof(Recipe.Id))!.SetValue(recipe, ObjectId.GenerateNewId().ToString());

            var restored = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Recipe>(recipe.ToBson());

            Assert.Equal(3, restored.Servings);
            Assert.Equal(24m, restored.Budget);
            Assert.Equal(["vegan", "no peanuts"], restored.DietaryRestrictions);
            Assert.Equal("Thai", restored.CuisinePreference);
        }

        [Fact]
        public void Legacy_saved_recipe_without_generation_context_still_deserializes()
        {
            var recipe = new Recipe("507f1f77bcf86cd799439011", "Old", [], 5m, "1. Cook.");
            typeof(Recipe).GetProperty(nameof(Recipe.Id))!.SetValue(recipe, ObjectId.GenerateNewId().ToString());
            var document = recipe.ToBsonDocument();
            document.Remove("Servings");
            document.Remove("Budget");
            document.Remove("DietaryRestrictions");
            document.Remove("CuisinePreference");

            var restored = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Recipe>(document);

            Assert.Null(restored.Servings);
            Assert.Null(restored.Budget);
            Assert.Null(restored.DietaryRestrictions);
            Assert.Null(restored.CuisinePreference);
        }

        [Fact]
        public void Missing_generation_metadata_is_not_saved_as_zero()
        {
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption { Name = "Old", Ingredients = ["beans"] }
            };

            var recipe = RecipeMapper.ToRecipe(context, "507f1f77bcf86cd799439011");

            Assert.Null(recipe.Servings);
            Assert.Null(recipe.Budget);
            Assert.Null(recipe.DietaryRestrictions);
            Assert.Null(recipe.CuisinePreference);
        }
    }
}
