using System;
using System.Collections.Generic;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// TERMINAL EXPRESSION — Food Allergy (Interpreter pattern).
    ///
    /// Takes a short allergen name (e.g. "seafood", "nuts") and expands it
    /// into every specific ingredient Gemma must avoid.
    /// </summary>
    public class AllergyExpression : IRestrictionExpression
    {
        private static readonly Dictionary<string, string> _expansions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["seafood"] = "salmon, tuna, cod, halibut, tilapia, shrimp, crab, " +
                            "lobster, clams, oysters, mussels, scallops, squid, " +
                            "anchovies, sardines — any aquatic animal",

                ["fish"] = "salmon, tuna, cod, halibut, tilapia, trout, bass, " +
                            "anchovies, sardines, herring — any fish species",

                ["shellfish"] = "shrimp, crab, lobster, clams, oysters, mussels, " +
                            "scallops, crayfish, barnacles",

                ["nuts"] = "peanuts, almonds, cashews, walnuts, pecans, " +
                            "pistachios, hazelnuts, macadamia, brazil nuts, pine nuts",

                ["peanuts"] = "peanuts, peanut butter, peanut oil, groundnuts",

                ["dairy"] = "milk, cheese, butter, cream, yogurt, sour cream, " +
                            "ice cream, whey, casein, lactose",

                ["eggs"] = "eggs, egg whites, egg yolks, mayonnaise, meringue, albumin",

                ["gluten"] = "wheat, barley, rye, regular flour, bread, pasta, " +
                            "crackers, couscous, semolina",

                ["soy"] = "soy sauce, tofu, edamame, miso, tempeh, soy milk, " +
                            "soybean oil",

                ["chicken"] = "chicken, all poultry including turkey and duck",

                ["pork"] = "pork, bacon, ham, lard, prosciutto, salami, sausage " +
                            "(unless explicitly beef or chicken)",

                ["beef"] = "beef, steak, ground beef, veal, brisket",

                ["sesame"] = "sesame seeds, sesame oil, tahini",

                ["sulfites"] = "wine, dried fruit, pickled foods, vinegar, " +
                            "pre-packaged potato products",
            };

        // Holds the raw allergen string extracted by the Gemma AI model (e.g., "dairy", "peanuts")
        private readonly string _allergen;

        // Constructor called when the Gemma parser builds the expression tree for the meal filters
        public AllergyExpression(string allergen) => _allergen = allergen;

        // Resolves the AI-extracted allergen into a formatted string for the UI team
        public string Interpret()
        {
            // Checks if the allergen Gemma flagged maps to a broader ingredient list.
            // E.g., If Gemma outputs "dairy", 'forbidden' expands to "milk, cheese, whey".
            // If Gemma detects a niche allergy not in the dictionary, it falls back to using the raw AI output.
            string forbidden = _expansions.TryGetValue(_allergen, out string? expansion)
                ? expansion
                : _allergen;

            // Formats the final warning string for the MAUI front-end to bind to a critical UI alert.
            // Uppercasing the AI-detected allergen ensures it immediately catches the user's eye in the recipe view.
            return $"ALLERGY — treat as DEADLY: {_allergen.ToUpper()} — " +
                   $"FORBIDDEN: {forbidden}.";
        }
    }
}