using Voba.Models;

namespace Voba.Interfaces
{
    public interface IPriceStrategy
    {
        List<Ingredient> Optimize(List<Ingredient> items, decimal budget);
    }
}
