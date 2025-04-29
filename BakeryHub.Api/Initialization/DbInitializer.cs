using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Api.Initialization;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var accountService = services.GetRequiredService<IAccountService>();
        var categoryService = services.GetRequiredService<ICategoryService>();
        var productService = services.GetRequiredService<IProductService>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        try
        {
            await EnsureRoleExistsAsync(roleManager, "Admin");
            await EnsureRoleExistsAsync(roleManager, "Customer");

            string defaultAdminEmail = "admin@admin.com";
            string defaultPassword = "qwe123QWE";
            string defaultSubdomain = "pasteleria";
            string defaultBusinessName = "Pastelería Deliciosa";

            Guid? tenantId = null;
            var existingAdmin = await userManager.FindByEmailAsync(defaultAdminEmail);
            var existingTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == defaultSubdomain);

            if (existingAdmin == null && existingTenant == null)
            {
                var adminDto = new AdminRegisterDto
                {
                    AdminName = "Admin Principal",
                    Email = defaultAdminEmail,
                    Password = defaultPassword,
                    ConfirmPassword = defaultPassword,
                    PhoneNumber = "87654321",
                    BusinessName = defaultBusinessName,
                    Subdomain = defaultSubdomain
                };
                var (result, userId) = await accountService.RegisterAdminAsync(adminDto);

                if (result.Succeeded && userId.HasValue)
                {
                    var newAdminUser = await userManager.FindByIdAsync(userId.Value.ToString());
                    tenantId = newAdminUser?.TenantId;

                    if (tenantId.HasValue)
                    {
                        var defaultCategories = new List<CreateCategoryDto> {
                             new CreateCategoryDto { Name = "Tortas Clasicas" }, new CreateCategoryDto { Name = "Masitas Secas" },
                             new CreateCategoryDto { Name = "Postres Frios" }, new CreateCategoryDto { Name = "Salados Horneados" },
                             new CreateCategoryDto { Name = "Bebidas Calientes" }, new CreateCategoryDto { Name = "Especialidades" },
                             new CreateCategoryDto { Name = "Tortas Premium" }
                          };
                        var createdCategoryIds = new Dictionary<string, Guid>();
                        foreach (var catDto in defaultCategories)
                        {
                            var createdCat = await categoryService.CreateCategoryForAdminAsync(catDto, tenantId.Value);
                            if (createdCat != null) { createdCategoryIds[catDto.Name] = createdCat.Id; }
                            else
                            {
                                var existingCat = await context.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId.Value && c.Name == catDto.Name);
                                if (existingCat != null) createdCategoryIds[catDto.Name] = existingCat.Id;
                            }
                        }

                        if (createdCategoryIds.Any())
                        {
                            var defaultProducts = GenerateDefaultProducts(createdCategoryIds);
                            foreach (var prodDto in defaultProducts)
                            {
                                prodDto.Images ??= new List<string>();
                                if (!prodDto.Images.Any()) { prodDto.Images.Add("https://res.cloudinary.com/dk6q93ryt/image/upload/v1714313823/samples/food/dessert.jpg"); }
                                if (prodDto.CategoryId != Guid.Empty && createdCategoryIds.ContainsValue(prodDto.CategoryId))
                                {
                                    await productService.CreateProductForAdminAsync(prodDto, tenantId.Value);
                                }
                            }
                        }
                    }
                }
                else { return; }
            }
            else
            {
                tenantId = existingTenant?.Id ?? existingAdmin?.TenantId;
                if (!tenantId.HasValue) { return; }
            }

            if (tenantId.HasValue)
            {
                string customerPassword = "qwe123QWE";
                for (int i = 1; i <= 10; i++)
                {
                    string customerEmail = $"cliente{i}@gmail.com";
                    string customerName = $"Cliente Ejemplo {i}";
                    string customerPhone = $"{i}{i}{i}{i}{i}{i}{i}{i}".PadLeft(8, '0');

                    var customerDto = new CustomerRegisterDto
                    {
                        Name = customerName,
                        Email = customerEmail,
                        Password = customerPassword,
                        ConfirmPassword = customerPassword,
                        PhoneNumber = customerPhone
                    };

                    await accountService.RegisterCustomerForTenantAsync(customerDto, tenantId.Value);
                    await Task.Delay(20);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<ApplicationRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }

    private static List<CreateProductDto> GenerateDefaultProducts(Dictionary<string, Guid> categoryIds)
    {
        var products = new List<CreateProductDto>();
        var random = new Random();
        string defaultImageUrl = "https://res.cloudinary.com/dk6q93ryt/image/upload/v1714313823/samples/food/dessert.jpg";

        Guid tortasClasicasId = categoryIds.GetValueOrDefault("Tortas Clasicas");
        Guid masitasId = categoryIds.GetValueOrDefault("Masitas Secas");
        Guid postresFriosId = categoryIds.GetValueOrDefault("Postres Frios");
        Guid saladosId = categoryIds.GetValueOrDefault("Salados Horneados");
        Guid bebidasId = categoryIds.GetValueOrDefault("Bebidas Calientes");
        Guid especialesId = categoryIds.GetValueOrDefault("Especialidades");
        Guid tortasPremiumId = categoryIds.GetValueOrDefault("Tortas Premium");

        var availableCategoryIds = categoryIds.Values.Where(id => id != Guid.Empty).ToList();
        if (!availableCategoryIds.Any()) return products;

        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta de Vainilla Clasica", Description = "Bizcocho esponjoso tradicional.", Price = 75.00m, CategoryId = tortasClasicasId, Images = new List<string> { defaultImageUrl } });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Alfajor Maicena (Unidad)", Description = "Relleno de dulce de leche y coco rallado.", Price = 4.50m, CategoryId = masitasId, Images = new List<string> { defaultImageUrl } });
        if (postresFriosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Mousse de Maracuyá Individual", Description = "Refrescante postre tropical.", Price = 15.00m, CategoryId = postresFriosId, Images = new List<string> { defaultImageUrl } });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Empanada de Pollo Jugosa", Description = "Con aceitunas y huevo.", Price = 7.00m, CategoryId = saladosId, Images = new List<string> { defaultImageUrl } });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Café Americano Recién Hecho", Description = "Grano seleccionado.", Price = 10.00m, CategoryId = bebidasId, Images = new List<string> { defaultImageUrl } });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto { Name = "Cheesecake Frutos Rojos Porción", Description = "Base de galleta, cremoso relleno y coulis.", Price = 25.00m, CategoryId = especialesId, Images = new List<string> { defaultImageUrl } });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Ópera Clásica", Description = "Capas de bizcocho, ganache y crema de café.", Price = 150.00m, CategoryId = tortasPremiumId, Images = new List<string> { defaultImageUrl } });

        var additionalProductData = new List<(string Name, string Desc, decimal Price, Guid CategoryId)>
        {
            ("Torta Selva Negra", "Bizcocho de chocolate, crema y cerezas.", 110.00m, tortasClasicasId),
            ("Galletas Surtidas (Caja Pequeña)", "Variedad de masitas secas.", 45.00m, masitasId),
            ("Tiramisú Clásico", "Postre italiano con café y mascarpone.", 22.00m, postresFriosId),
            ("Cuñapé Horneado (Unidad)", "Pan de queso tradicional.", 5.00m, saladosId),
            ("Chocolate Caliente Espeso", "Ideal para días fríos.", 18.00m, bebidasId),
            ("Volcán de Chocolate", "Con centro líquido.", 28.00m, especialesId),
            ("Torta Red Velvet", "Terciopelo rojo con frosting de queso crema.", 130.00m, tortasPremiumId),
            ("Empanada de Carne Suave", "Relleno tradicional.", 7.50m, saladosId)
        };

        int productsNeeded = 15 - products.Count;
        for (int i = 0; i < productsNeeded && i < additionalProductData.Count; i++)
        {
            var data = additionalProductData[i];
            Guid targetCategoryId = data.CategoryId;
            if (targetCategoryId == Guid.Empty || !availableCategoryIds.Contains(targetCategoryId))
            {
                targetCategoryId = availableCategoryIds[random.Next(availableCategoryIds.Count)];
            }
            products.Add(new CreateProductDto
            {
                Name = data.Name,
                Description = data.Desc,
                Price = data.Price,
                CategoryId = targetCategoryId,
                Images = new List<string> { defaultImageUrl }
            });
        }
        return products.Take(15).ToList();
    }
}
