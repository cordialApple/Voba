using System.Collections.Generic;
using Voba.AI.Interpreter;
using Xunit;

namespace Voba.Core.Tests
{
    // The interpreter turns user dietary input into the forbidden-ingredient rule
    // block fed to Gemma. It is safety-critical: a misclassification can let an
    // allergen through, so the classification and rule text are pinned here.
    public class RestrictionParserTests
    {
        [Fact]
        public void Empty_list_returns_none_expression()
        {
            var expr = RestrictionParser.Parse(new List<string>());

            Assert.IsType<NoneExpression>(expr);
            Assert.Equal("No dietary restrictions or allergies.", expr.Interpret());
        }

        [Fact]
        public void Blank_and_whitespace_entries_are_dropped()
        {
            var expr = RestrictionParser.Parse(new List<string> { "   ", "", "\t" });

            Assert.IsType<NoneExpression>(expr);
        }

        [Fact]
        public void Single_known_diet_maps_to_diet_expression()
        {
            var expr = RestrictionParser.Parse(new List<string> { "vegan" });

            Assert.IsType<DietExpression>(expr);
            var text = expr.Interpret();
            Assert.Contains("DIET: Vegan", text);
            Assert.Contains("honey", text);
        }

        [Fact]
        public void Diet_spelling_variants_are_normalised()
        {
            Assert.Contains("Gluten-Free",
                RestrictionParser.Parse(new List<string> { "gluten free" }).Interpret());

            // "ketogenic" normalises to "keto" and resolves to the keto rule.
            Assert.Contains("high fat, moderate protein, low carb",
                RestrictionParser.Parse(new List<string> { "ketogenic" }).Interpret());
        }

        [Fact]
        public void Known_allergen_maps_to_allergy_expression_with_deadly_warning()
        {
            var expr = RestrictionParser.Parse(new List<string> { "peanuts" });

            Assert.IsType<AllergyExpression>(expr);
            var text = expr.Interpret();
            Assert.Contains("treat as DEADLY: PEANUTS", text);
            Assert.Contains("peanut butter", text);
        }

        [Fact]
        public void Unrecognised_restriction_falls_back_to_unknown_expression()
        {
            var expr = RestrictionParser.Parse(new List<string> { "carnivore" });

            Assert.IsType<UnknownRestrictionExpression>(expr);
            Assert.Contains("RESTRICTION: carnivore", expr.Interpret());
        }

        [Fact]
        public void Multiple_restrictions_combine_into_and_expression()
        {
            var expr = RestrictionParser.Parse(new List<string> { "vegan", "peanuts" });

            Assert.IsType<AndExpression>(expr);
            var text = expr.Interpret();
            Assert.Contains("DIET: Vegan", text);
            Assert.Contains("treat as DEADLY: PEANUTS", text);
            // Each rule is emitted on its own line for the prompt.
            Assert.Contains("\n", text);
        }

        [Fact]
        public void Blank_entries_alongside_a_real_rule_collapse_to_a_single_terminal()
        {
            var expr = RestrictionParser.Parse(new List<string> { "  ", "vegan" });

            Assert.IsType<DietExpression>(expr);
            Assert.DoesNotContain("\n", expr.Interpret());
        }
    }
}
