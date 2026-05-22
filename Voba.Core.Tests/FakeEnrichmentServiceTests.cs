using System;
using System.Threading.Tasks;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests
{
    // The offline fake drives the full flow with no API key/network. Its output must
    // stay deterministic (count-derived cost + per-serving nutrition) so demos and the
    // pipeline tests behave predictably.
    public class FakeEnrichmentServiceTests
    {
        [Fact]
        public async Task Computes_total_and_per_serving_values()
        {
            var service = new FakeEnrichmentService();

            var result = await service.EnrichAsync(new[] { "a", "b", "c", "d" }, servings: 2);

            Assert.NotNull(result);
            Assert.Equal(5.00m, result!.TotalCostUsd);     // 4 * 1.25
            Assert.Equal(2.50m, result.CostPerServingUsd); // 5.00 / 2
            Assert.Equal(190m, result.Nutrition.Calories); // 4 * 95 / 2
            Assert.Equal(12.0m, result.Nutrition.ProteinGrams);
            Assert.Equal(8.0m, result.Nutrition.FatGrams);
            Assert.Equal(22.0m, result.Nutrition.CarbGrams);
        }

        [Fact]
        public async Task Blank_or_empty_ingredients_return_null()
        {
            var service = new FakeEnrichmentService();

            Assert.Null(await service.EnrichAsync(new[] { "  ", "" }, 2));
            Assert.Null(await service.EnrichAsync(Array.Empty<string>(), 2));
        }

        [Fact]
        public async Task Non_positive_servings_are_treated_as_one()
        {
            var service = new FakeEnrichmentService();

            var result = await service.EnrichAsync(new[] { "a", "b" }, servings: 0);

            Assert.NotNull(result);
            Assert.Equal(2.50m, result!.TotalCostUsd);
            Assert.Equal(2.50m, result.CostPerServingUsd); // divided by 1, not 0
            Assert.Equal(190m, result.Nutrition.Calories); // 2 * 95 / 1
        }
    }
}
