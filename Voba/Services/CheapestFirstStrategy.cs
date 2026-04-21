using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class CheapestFirstStrategy : IPriceStrategy
    {
        public List<Ingredient> Optimize(List<Ingredient> items, decimal budget)
        {
            var result  = new List<Ingredient>();
            var running = 0m;

            foreach (var item in items.OrderBy(i => i.EstimatedCost))
            {
                if (running + item.EstimatedCost > budget) break;
                result.Add(item);
                running += item.EstimatedCost;
            }

            return result;
        }
    }
}
