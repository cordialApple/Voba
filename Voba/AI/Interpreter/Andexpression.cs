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
        private readonly List<IRestrictionExpression> _expressions;

        public AndExpression(List<IRestrictionExpression> expressions)
        {
            _expressions = expressions;
        }

        public string Interpret() =>
            string.Join("\n", _expressions.Select(e => e.Interpret()));
    }
}