namespace Voba.AI.Interpreter
{
    /// <summary>
    /// ABSTRACT EXPRESSION (Interpreter pattern).
    ///
    /// Every dietary restriction — whether a lifestyle diet, a food allergy,
    /// a combination, or nothing at all — implements this interface.
    ///
    /// Interpret() produces a precise, Gemma-ready rule string that replaces
    /// the raw user input previously pasted directly into the prompt.
    /// </summary>
    public interface IRestrictionExpression
    {
        string Interpret();
    }
}