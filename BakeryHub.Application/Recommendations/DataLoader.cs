using BakeryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace BakeryHub.Application.Recommendations;

public class DataLoader
{
    private readonly MLContext _mlContext;
    private readonly ApplicationDbContext _dbContext;

    public DataLoader(MLContext mlContext, ApplicationDbContext dbContext)
    {
        _mlContext = mlContext;
        _dbContext = dbContext;
    }

    public async Task<DataMappings?> LoadMappingsAndHistoryForTenantAsync(Guid tenantId)
    {
        var mappings = new DataMappings();
        float nextUserFloatId = 1.0f;
        int nextProductIntId = 1;
        int nextCategoryIntId = 1;

        var tenantOrders = await _dbContext.Orders
            .Where(o => o.TenantId == tenantId && o.ApplicationUserId.HasValue)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
            .AsNoTracking()
            .ToListAsync();

        if (!tenantOrders.Any())
            return null;

        foreach (var order in tenantOrders)
        {
            Guid userIdGuid = order.ApplicationUserId!.Value;

            if (!mappings.UserGuidToFloatMap.ContainsKey(userIdGuid))
            {
                mappings.UserGuidToFloatMap[userIdGuid] = nextUserFloatId++;
            }

            if (!mappings.UserPurchaseHistory.ContainsKey(userIdGuid))
            {
                mappings.UserPurchaseHistory[userIdGuid] = new HashSet<Guid>();
            }

            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    mappings.UserPurchaseHistory[userIdGuid].Add(item.Product.Id);
                }
            }
        }

        if (!mappings.UserGuidToFloatMap.Any())
            return null;

        var allProductsInTenantOrders = tenantOrders
            .SelectMany(o => o.OrderItems)
            .Select(oi => oi.Product)
            .Where(p => p != null)
            .DistinctBy(p => p!.Id)
            .ToList();

        if (!allProductsInTenantOrders.Any())
            return null;

        foreach (var product in allProductsInTenantOrders)
        {
            if (!mappings.ProductGuidToIntMap.ContainsKey(product!.Id))
            {
                int productIntId = nextProductIntId++;
                mappings.ProductGuidToIntMap[product.Id] = productIntId;
                mappings.IntToProductGuidMap[productIntId] = product.Id;


                if (product.Category != null)
                {
                    if (!mappings.CategoryGuidToIntMap.ContainsKey(product.CategoryId))
                    {
                        int categoryIntId = nextCategoryIntId++;
                        mappings.CategoryGuidToIntMap[product.CategoryId] = categoryIntId;
                        mappings.IntToCategoryGuidMap[categoryIntId] = product.CategoryId;
                    }
                    mappings.ProductIntToCategoryIntMap[productIntId] = mappings.CategoryGuidToIntMap[product.CategoryId];
                }
                else
                {

                }
                {
                    if (!mappings.IntToCategoryGuidMap.ContainsKey(0))
                    {
                        mappings.CategoryGuidToIntMap[Guid.Empty] = 0;
                        mappings.IntToCategoryGuidMap[0] = Guid.Empty;
                    }
                    mappings.ProductIntToCategoryIntMap[productIntId] = 0;
                }
            }
        }

        if (!mappings.IntToCategoryGuidMap.ContainsKey(0) && mappings.CategoryGuidToIntMap.Any(kvp => kvp.Value == 0))
        {
            mappings.CategoryGuidToIntMap.TryAdd(Guid.Empty, 0);
            mappings.IntToCategoryGuidMap.TryAdd(0, Guid.Empty);
        }
        else if (!mappings.CategoryGuidToIntMap.Any() && !mappings.IntToCategoryGuidMap.ContainsKey(0))
        {
            mappings.CategoryGuidToIntMap[Guid.Empty] = 0;
            mappings.IntToCategoryGuidMap[0] = Guid.Empty;
        }


        if (!mappings.ProductGuidToIntMap.Any())
            return null;

        return mappings;
    }

    public IDataView LoadDataForTenant(DataMappings tenantMappings)
    {
        var trainingData = new List<ProductRating>();
        if (tenantMappings == null || !tenantMappings.UserPurchaseHistory.Any() || !tenantMappings.IntToProductGuidMap.Any())
        {
            return _mlContext.Data.LoadFromEnumerable(trainingData);
        }

        var allProductIntIdsInTenant = tenantMappings.IntToProductGuidMap.Keys.ToList();

        foreach (var userEntry in tenantMappings.UserPurchaseHistory)
        {
            Guid userIdGuid = userEntry.Key;
            HashSet<Guid> purchasedProductGuids = userEntry.Value;

            if (!tenantMappings.UserGuidToFloatMap.TryGetValue(userIdGuid, out float userFloatId)) continue;

            var purchasedProductIntIds = new HashSet<int>(
                purchasedProductGuids
                    .Select(guid => tenantMappings.ProductGuidToIntMap.TryGetValue(guid, out int id) ? id : -1)
                    .Where(id => id != -1)
            );

            foreach (int purchasedIntId in purchasedProductIntIds)
            {
                if (tenantMappings.ProductIntToCategoryIntMap.TryGetValue(purchasedIntId, out int categoryIntId))
                {
                    trainingData.Add(new ProductRating
                    {
                        UserId = userFloatId,
                        ProductId = purchasedIntId,
                        CategoryId = categoryIntId,
                        Label = true
                    });
                }
            }

            foreach (int productIntId in allProductIntIdsInTenant)
            {
                if (!purchasedProductIntIds.Contains(productIntId))
                {
                    if (tenantMappings.ProductIntToCategoryIntMap.TryGetValue(productIntId, out int categoryIntId))
                    {
                        trainingData.Add(new ProductRating
                        {
                            UserId = userFloatId,
                            ProductId = productIntId,
                            CategoryId = categoryIntId,
                            Label = false
                        });
                    }
                }
            }
        }

        if (!trainingData.Any())
        {
            return _mlContext.Data.LoadFromEnumerable(new List<ProductRating>());
        }
        return _mlContext.Data.LoadFromEnumerable(trainingData);
    }
}
