using Microsoft.ML;

namespace BakeryHub.Application.Recommendations;

public class DataLoader
{
    private readonly MLContext _mlContext;
    private readonly Dictionary<Guid, Guid> _productToCategoryMap;

    public DataLoader(MLContext mlContext)
    {
        _mlContext = mlContext;
        _productToCategoryMap = InitializeProductToCategoryMapping();
    }

    private Dictionary<Guid, Guid> InitializeProductToCategoryMapping()
    {
        var categoryIds = new Dictionary<string, Guid> {
                {"Bebidas Calientes", Guid.Parse("f1f258e1-5dd4-4a9a-8623-2e4b5159949d")},
                {"Especialidades", Guid.Parse("c2e6f8ce-f0d8-42e0-a304-db6f1dfc382f")},
                {"Masitas Secas", Guid.Parse("4624f00d-44d4-45ef-b6e3-ea575e018758")},
                {"Postres Frios", Guid.Parse("d7cbb8d0-7ee5-4c1d-a359-b3da0eb018cd")},
                {"Salados Horneados", Guid.Parse("b33a3f57-dc2f-4f1d-a98f-c701f06bf600")},
                {"Tortas Clasicas", Guid.Parse("88d349eb-da70-486f-8734-4db45fe0f539")},
                {"Tortas Premium", Guid.Parse("f2118883-5811-4662-a4ec-a50c10f7eae5")}
            };

        var productIds = new Dictionary<string, Guid> {
                {"Torta de Vainilla Clasica", Guid.Parse("2b3f1a8a-7b51-4608-bd4e-efaa3fa23ef2")},
                {"Alfajor Maicena (Unidad)", Guid.Parse("eaaf7c4b-4bb7-484d-9367-01960387d96b")},
                {"Mousse de Maracuyá Individual", Guid.Parse("83c1fb54-676b-4084-a810-5dd46355c313")},
                {"Empanada de Pollo Jugosa", Guid.Parse("5f6f2920-2007-436f-b399-c46605e90bda")},
                {"Café Americano Recién Hecho", Guid.Parse("33a75043-e56d-41b6-aa69-a93ddbb10e91")},
                {"Cheesecake Frutos Rojos Porción", Guid.Parse("ce41d1d8-b145-4627-81e4-6072c7518080")},
                {"Torta Ópera Clásica", Guid.Parse("d532fa61-32b2-4f6b-8f0d-80abc6e217e7")},
                {"Torta Selva Negra", Guid.Parse("5d279631-ba08-42c4-a2af-a5b935953ca0")},
                {"Galletas Surtidas (Caja Pequeña)", Guid.Parse("6bb949c2-3f19-40b0-a79e-a37205b580c6")},
                {"Tiramisú Clásico", Guid.Parse("752ef6e7-61ba-4f99-84cf-01aa1f9be958")},
                {"Cuñapé Horneado (Unidad)", Guid.Parse("3551ab00-11ef-4b88-b600-7c2fa92633d3")},
                {"Chocolate Caliente Espeso", Guid.Parse("0b17c216-fd61-4b7e-a8bc-21c54e78b482")},
                {"Volcán de Chocolate", Guid.Parse("b7656089-6be8-4579-a29d-4b348b37828f")},
                {"Torta Red Velvet", Guid.Parse("9f98e9dd-46bb-4304-960f-9e256e9f4e72")},
                {"Empanada de Carne Suave", Guid.Parse("4cc4439d-2447-432b-a5e4-3cacd827536e")}
            };

        return new Dictionary<Guid, Guid>
            {
                { productIds["Torta de Vainilla Clasica"], categoryIds["Tortas Clasicas"] },
                { productIds["Alfajor Maicena (Unidad)"], categoryIds["Masitas Secas"] },
                { productIds["Mousse de Maracuyá Individual"], categoryIds["Postres Frios"] },
                { productIds["Empanada de Pollo Jugosa"], categoryIds["Salados Horneados"] },
                { productIds["Café Americano Recién Hecho"], categoryIds["Bebidas Calientes"] },
                { productIds["Cheesecake Frutos Rojos Porción"], categoryIds["Especialidades"] },
                { productIds["Torta Ópera Clásica"], categoryIds["Tortas Premium"] },
                { productIds["Torta Selva Negra"], categoryIds["Tortas Clasicas"] },
                { productIds["Galletas Surtidas (Caja Pequeña)"], categoryIds["Masitas Secas"] },
                { productIds["Tiramisú Clásico"], categoryIds["Postres Frios"] },
                { productIds["Cuñapé Horneado (Unidad)"], categoryIds["Salados Horneados"] },
                { productIds["Chocolate Caliente Espeso"], categoryIds["Bebidas Calientes"] },
                { productIds["Volcán de Chocolate"], categoryIds["Especialidades"] },
                { productIds["Torta Red Velvet"], categoryIds["Tortas Premium"] },
                { productIds["Empanada de Carne Suave"], categoryIds["Salados Horneados"] }
            };
    }

    public DataMappings? LoadMappingsAndHistory(string purchasesPath, string purchaseDetailsPath)
    {
        if (!File.Exists(purchasesPath) || !File.Exists(purchaseDetailsPath))
        {
            return null;
        }

        try
        {
            var mappings = new DataMappings();
            float nextUserFloatId = 1.0f;
            int nextProductIntId = 1;
            int nextCategoryIntId = 1;

            var uniqueProductGuids = new HashSet<Guid>();
            var uniqueCategoryGuids = new HashSet<Guid>();

            var detailLines = File.ReadAllLines(purchaseDetailsPath).Skip(1).ToList();

            foreach (var line in detailLines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && Guid.TryParse(parts[1], out Guid productGuid))
                {
                    uniqueProductGuids.Add(productGuid);
                    if (_productToCategoryMap.TryGetValue(productGuid, out Guid categoryGuid))
                    {
                        uniqueCategoryGuids.Add(categoryGuid);
                    }
                }
            }

            foreach (var productGuid in uniqueProductGuids)
            {
                if (!mappings.ProductGuidToIntMap.ContainsKey(productGuid))
                {
                    int productIntId = nextProductIntId++;
                    mappings.ProductGuidToIntMap[productGuid] = productIntId;
                    mappings.IntToProductGuidMap[productIntId] = productGuid;
                }
            }

            if (!mappings.ProductGuidToIntMap.Any())
            {
                return null;
            }

            foreach (var categoryGuid in uniqueCategoryGuids)
            {
                if (!mappings.CategoryGuidToIntMap.ContainsKey(categoryGuid))
                {
                    int categoryIntId = nextCategoryIntId++;
                    mappings.CategoryGuidToIntMap[categoryGuid] = categoryIntId;
                    mappings.IntToCategoryGuidMap[categoryIntId] = categoryGuid;
                }
            }

            if (!mappings.IntToCategoryGuidMap.ContainsKey(0))
            {
                mappings.CategoryGuidToIntMap[Guid.Empty] = 0;
                mappings.IntToCategoryGuidMap[0] = Guid.Empty;
            }

            foreach (var kvp in mappings.ProductGuidToIntMap)
            {
                Guid productGuid = kvp.Key;
                int productIntId = kvp.Value;
                if (_productToCategoryMap.TryGetValue(productGuid, out Guid categoryGuid) &&
                    mappings.CategoryGuidToIntMap.TryGetValue(categoryGuid, out int categoryIntId))
                {
                    mappings.ProductIntToCategoryIntMap[productIntId] = categoryIntId;
                }
                else
                {
                    mappings.ProductIntToCategoryIntMap[productIntId] = 0;
                }
            }

            var purchasesLines = File.ReadAllLines(purchasesPath).Skip(1).ToList();

            var purchaseIdToProducts = new Dictionary<int, List<Guid>>();
            foreach (var line in detailLines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int purchaseId) && Guid.TryParse(parts[1], out Guid productGuid))
                {
                    if (mappings.ProductGuidToIntMap.ContainsKey(productGuid))
                    {
                        if (!purchaseIdToProducts.ContainsKey(purchaseId))
                        {
                            purchaseIdToProducts[purchaseId] = new List<Guid>();
                        }
                        purchaseIdToProducts[purchaseId].Add(productGuid);
                    }
                }
            }

            foreach (var line in purchasesLines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2 || !int.TryParse(parts[0], out int purchaseId) || !Guid.TryParse(parts[1], out Guid userId))
                {
                    continue;
                }

                if (!mappings.UserGuidToFloatMap.ContainsKey(userId))
                {
                    mappings.UserGuidToFloatMap[userId] = nextUserFloatId++;
                }

                if (purchaseIdToProducts.TryGetValue(purchaseId, out var productsInPurchase))
                {
                    if (!mappings.UserPurchaseHistory.ContainsKey(userId))
                    {
                        mappings.UserPurchaseHistory[userId] = new HashSet<Guid>();
                    }
                    foreach (var productGuid in productsInPurchase)
                    {
                        if (mappings.ProductGuidToIntMap.ContainsKey(productGuid))
                        {
                            mappings.UserPurchaseHistory[userId].Add(productGuid);
                        }
                    }
                }
            }

            return mappings;
        }
        catch
        {
            return null;
        }
    }

    public IDataView LoadData(string purchasesPath, string purchaseDetailsPath, DataMappings mappings)
    {
        var trainingData = new List<ProductRating>();

        var allProductIntIds = mappings.IntToProductGuidMap.Keys.ToList();

        foreach (var userEntry in mappings.UserPurchaseHistory)
        {
            Guid userIdGuid = userEntry.Key;
            HashSet<Guid> purchasedProductGuids = userEntry.Value;

            if (!mappings.UserGuidToFloatMap.TryGetValue(userIdGuid, out float userFloatId)) continue;

            var purchasedProductIntIds = new HashSet<int>(
                purchasedProductGuids.Select(guid => mappings.ProductGuidToIntMap.TryGetValue(guid, out int id) ? id : -1)
                                     .Where(id => id != -1)
            );

            foreach (int purchasedIntId in purchasedProductIntIds)
            {
                if (mappings.ProductIntToCategoryIntMap.TryGetValue(purchasedIntId, out int categoryIntId))
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

            foreach (int productIntId in allProductIntIds)
            {
                if (!purchasedProductIntIds.Contains(productIntId))
                {
                    if (mappings.ProductIntToCategoryIntMap.TryGetValue(productIntId, out int categoryIntId))
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
