using System;
using System.Collections.Generic;
using System.Linq;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// CLIENT / PARSER (Interpreter pattern).
    ///
    /// Turns a raw List from the UI into an expression tree.
    /// The tree is then evaluated by calling Interpret() to produce
    /// a structured rule block ready for the Gemma prompt.
    ///
    /// Classification logic lives here — not in the handlers, not in the prompt.
    ///
    /// Usage:
    ///   var expression = RestrictionParser.Parse(context.DietaryRestrictions);
    ///   string rules   = expression.Interpret();
    ///   // Inject `rules` into prompt.
    /// </summary>
    public static class RestrictionParser
    {
        // Restrictions that map to DietExpression
        private static readonly HashSet<string> _knownDiets =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "vegan", "vegetarian", "keto", "ketogenic",
            "paleo", "gluten-free", "gluten free",
            "dairy-free", "dairy free", "halal", "kosher"
        };

        // Restrictions that map to AllergyExpression
        private static readonly HashSet<string> _knownAllergens =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "seafood", "fish", "shellfish", "nuts", "tree nuts", "nut",
            "peanuts", "peanut", "dairy", "eggs", "egg", "gluten",
            "soy", "soya", "chicken", "pork", "beef", "sesame", "sulfites"
        };

        /// <summary>
        /// Parses the restriction list into an expression tree.
        /// Returns NoneExpression if the list is empty.
        /// Returns a single terminal if the list has one entry.
        /// Returns an AndExpression if the list has multiple entries.
        /// </summary>

        // Takes the raw list of food rules found by the Gemma AI model and builds the expression tree.
        public static IRestrictionExpression Parse(List<string> restrictions)
        {
            // Cleans the raw text from the AI output.
            // Removes extra spaces and drops any empty or blank lines to prevent errors in the app.
            var cleaned = restrictions
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            // Returns an empty rule if the AI found zero diets or allergies.
            // Helps the MAUI front-end know there are no warnings to display on the recipe screen.
            if (cleaned.Count == 0)
                return new NoneExpression();

            // Turns each cleaned string into the correct expression type (like DietExpression or AllergyExpression).
            // Uses the Classify method to figure out exactly what category the AI detected.
            var expressions = cleaned.Select(Classify).ToList();

            // Returns a single rule directly if only one was found.
            // If the AI found multiple rules, bundles them all together into an AndExpression.
            return expressions.Count == 1
                ? expressions[0]
                : new AndExpression(expressions);
        }

        // Looks at a single rule found by the Gemma AI and decides what kind of expression to create.
        private static IRestrictionExpression Classify(string restriction)
        {
            // Cleans up common spelling variations from the AI output before checking the lists.
            // Changes phrases like "gluten free" to "gluten-free" so the dictionary lookups work perfectly.
            string normalised = restriction
                .Replace("gluten free", "gluten-free")
                .Replace("dairy free", "dairy-free")
                .Replace("ketogenic", "keto");

            // Checks the predefined diet list. 
            // If a match is found, turns the AI text into a DietExpression.
            if (_knownDiets.Contains(normalised))
                return new DietExpression(normalised);

            // Checks the predefined allergy list. 
            // If a match is found, turns the AI text into an AllergyExpression.
            if (_knownAllergens.Contains(normalised))
                return new AllergyExpression(normalised);

            // Handles unexpected or rare rules from Gemma that do not fit into the known lists.
            // Wraps the raw text in an UnknownRestrictionExpression so the MAUI front-end can still display a basic safety warning.
            return new UnknownRestrictionExpression(restriction);
        }
    }
}