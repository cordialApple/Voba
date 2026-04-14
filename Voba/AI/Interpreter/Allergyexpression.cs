using System;
using System.Collections.Generic;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// TERMINAL EXPRESSION — Food Allergy (Interpreter pattern).
    ///
    /// Takes a short allergen name (e.g. "seafood", "nuts") and expands it
    /// into every specific ingredient Gemma must avoid.
    ///
    /// This replaces the hardcoded FORBIDDEN rules previously written
    /// directly into the GemmaIdeationHandler prompt — those rules only
    /// covered "seafood" and "fish". This class covers all common allergens
    /// and is trivially extensible.
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

        private readonly string _allergen;

        public AllergyExpression(string allergen) => _allergen = allergen;

        public string Interpret()
        {
            string forbidden = _expansions.TryGetValue(_allergen, out string? expansion)
                ? expansion
                : _allergen;

            return $"ALLERGY — treat as DEADLY: {_allergen.ToUpper()} — " +
                   $"FORBIDDEN: {forbidden}.";
        }
    }
}