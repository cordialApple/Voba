using Voba.Models;

namespace Voba.Interfaces
{
    public interface IPriceStrategy
    {
        /// <summary>Selects ingredients to include within the given budget.</summary>
        List<Ingredient> Optimize(List<Ingredient> items, decimal budget);
    }
}
