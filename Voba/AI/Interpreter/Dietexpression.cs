using System;
using System.Collections.Generic;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// TERMINAL EXPRESSION — Lifestyle Diet (Interpreter pattern).
    ///
    /// Recognises named diets and maps each to a precise forbidden-ingredient
    /// rule that Gemma 3:4b can follow reliably.
    ///
    /// Adding a new diet means adding one entry to _dietRules — no prompt
    /// logic, no handler code, and no other class changes.
    /// </summary>
    public class DietExpression : IRestrictionExpression
    {
        // Maps common diet names from the Gemma AI to a strict list of rules.
        // Ignores upper and lower case differences, so "Vegan" matches "vegan".
        private static readonly Dictionary<string, string> _dietRules =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["vegan"] = "DIET: Vegan — FORBIDDEN: all meat, all poultry, all fish, " +
                              "all seafood, all dairy, eggs, honey, gelatin.",

                ["vegetarian"] = "DIET: Vegetarian — FORBIDDEN: all meat, all poultry, " +
                              "all fish, all seafood.",

                ["keto"] = "DIET: Keto — FORBIDDEN: grains, bread, pasta, rice, sugar, " +
                              "potatoes, corn, beans, high-carb fruit (bananas, grapes, mangoes). " +
                              "REQUIRED: high fat, moderate protein, low carb.",

                ["paleo"] = "DIET: Paleo — FORBIDDEN: grains, legumes, dairy, refined sugar, " +
                              "processed food, vegetable oils.",

                ["gluten-free"] = "DIET: Gluten-Free — FORBIDDEN: wheat, barley, rye, regular flour, " +
                              "regular bread, regular pasta, regular crackers, soy sauce " +
                              "(unless labelled gluten-free).",

                ["dairy-free"] = "DIET: Dairy-Free — FORBIDDEN: milk, cheese, butter, cream, " +
                              "yogurt, ice cream, whey, casein.",

                ["halal"] = "DIET: Halal — FORBIDDEN: pork, pork products, alcohol, " +
                              "any non-halal meat.",

                ["kosher"] = "DIET: Kosher — FORBIDDEN: pork, shellfish, mixing meat and dairy " +
                              "in the same dish.",
            };

        private readonly string _diet;

        // Called when the Gemma parser builds the expression tree for a user's meal plan.
        public DietExpression(string diet) => _diet = diet;

        // Turns the AI-extracted diet into a formatted string for the MAUI front-end team to display.
        public string Interpret()
        {
            // Checks if the diet Gemma found matches a known rule in the dictionary.
            // If it matches, it returns the detailed forbidden list.
            // If Gemma finds a rare diet not in the dictionary, it falls back to a general safety warning.
            return _dietRules.TryGetValue(_diet, out string? rule)
                ? rule
                : $"DIET: {_diet} — strictly comply with all standard rules of this diet.";
        }
    }
}