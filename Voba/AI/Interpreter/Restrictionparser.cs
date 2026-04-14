using System;
using System.Collections.Generic;
using System.Linq;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// CLIENT / PARSER (Interpreter pattern).
    ///
    /// Turns a raw List&lt;string&gt; from the UI into an expression tree.
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
        public static IRestrictionExpression Parse(List<string> restrictions)
        {
            var cleaned = restrictions
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            if (cleaned.Count == 0)
                return new NoneExpression();

            var expressions = cleaned.Select(Classify).ToList();

            return expressions.Count == 1
                ? expressions[0]
                : new AndExpression(expressions);
        }

        private static IRestrictionExpression Classify(string restriction)
        {
            // Normalise common variants before lookup
            string normalised = restriction
                .Replace("gluten free", "gluten-free")
                .Replace("dairy free", "dairy-free")
                .Replace("ketogenic", "keto");

            if (_knownDiets.Contains(normalised))
                return new DietExpression(normalised);

            if (_knownAllergens.Contains(normalised))
                return new AllergyExpression(normalised);

            // Unknown input: pass through with a generic compliance rule
            return new UnknownRestrictionExpression(restriction);
        }
    }
}