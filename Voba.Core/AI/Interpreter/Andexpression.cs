using System.Collections.Generic;
using System.Linq;

namespace Voba.AI.Interpreter
{
    /// <summary>
    /// NON-TERMINAL EXPRESSION — Combines restrictions (Interpreter pattern).
    ///
    /// When a user enters multiple restrictions (e.g. "Vegan, Nuts, Gluten-Free"),
    /// RestrictionParser wraps them in an AndExpression. Interpret() produces
    /// a single rule block with each restriction on its own line.
    ///
    /// Example output:
    ///
    ///   DIET: Vegan — FORBIDDEN: all meat, poultry, fish, dairy, eggs, honey.
    ///   ALLERGY — treat as DEADLY: NUTS — FORBIDDEN: peanuts, almonds, cashews...
    ///   DIET: Gluten-Free — FORBIDDEN: wheat, barley, rye, regular flour...
    /// </summary>
    public class AndExpression : IRestrictionExpression
    {
        // Holds the list of rules (like allergies or diets) that Gemma found.
        private readonly List<IRestrictionExpression> _expressions;

        // Used when Gemma finds more than one rule for a user. 
        // For example, if Gemma sees "vegan and allergic to dairy", both rules are passed here.
        public AndExpression(List<IRestrictionExpression> expressions)
        {
            _expressions = expressions;
        }

        // Gets the text from every rule and joins them together.
        // Puts each warning on a new line (\n) so the UI team can easily display them in a list on the screen.
        public string Interpret() =>
            string.Join("\n", _expressions.Select(e => e.Interpret()));
    }
}