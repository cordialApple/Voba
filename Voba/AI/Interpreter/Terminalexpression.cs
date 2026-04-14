namespace Voba.AI.Interpreter
{
    /// <summary>
    /// TERMINAL EXPRESSION — No restrictions (Interpreter pattern).
    /// Used when the user left the dietary field blank.
    /// </summary>
    public class NoneExpression : IRestrictionExpression
    {
        public string Interpret() => "No dietary restrictions or allergies.";
    }

    /// <summary>
    /// TERMINAL EXPRESSION — Unrecognised input (Interpreter pattern).
    /// Fallback for any restriction the parser could not classify as a
    /// known diet or allergen. Passes the raw text to Gemma with a
    /// generic compliance instruction.
    /// </summary>
    public class UnknownRestrictionExpression : IRestrictionExpression
    {
        private readonly string _raw;

        public UnknownRestrictionExpression(string raw) => _raw = raw;

        public string Interpret() =>
            $"RESTRICTION: {_raw} — strictly avoid any ingredient that conflicts with this.";
    }
}