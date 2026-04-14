using System;
using System.Collections.Generic;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// TERMINAL EXPRESSION — Lifestyle Diet (Interpreter pattern).
    ///
    /// Recognises named diets and maps each to a precise forbidden-ingredient
    /// rule that Gemma 3:1b can follow reliably.
    ///
    /// Adding a new diet means adding one entry to _dietRules — no prompt
    /// logic, no handler code, and no other class changes.
    /// </summary>
    public class DietExpression : IRestrictionExpression
    {
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

        public DietExpression(string diet) => _diet = diet;

        public string Interpret()
        {
            return _dietRules.TryGetValue(_diet, out string? rule)
                ? rule
                : $"DIET: {_diet} — strictly comply with all standard rules of this diet.";
        }
    }
}