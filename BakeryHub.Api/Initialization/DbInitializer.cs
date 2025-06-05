using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Enums;
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
        var random = new Random();

        try
        {
            await EnsureRoleExistsAsync(roleManager, "Admin");
            await EnsureRoleExistsAsync(roleManager, "Customer");

            string defaultAdminEmail = "luis@gmail.com";
            string defaultPassword = "qwe123QWE";
            string defaultSubdomain = "bliss-cake";
            string defaultBusinessName = "Bliss Cake bakery";
            string adminName = "Luis Espinoza";
            string adminPhoneNumber = "78318485";

            Guid? tenantId = null;
            ApplicationUser? adminUser = await userManager.FindByEmailAsync(defaultAdminEmail);
            Tenant? existingTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == defaultSubdomain);

            if (adminUser == null && existingTenant == null)
            {
                var adminDto = new AdminRegisterDto
                {
                    AdminName = adminName,
                    Email = defaultAdminEmail,
                    Password = defaultPassword,
                    ConfirmPassword = defaultPassword,
                    PhoneNumber = adminPhoneNumber,
                    BusinessName = defaultBusinessName,
                    Subdomain = defaultSubdomain
                };
                var (result, userId) = await accountService.RegisterAdminAsync(adminDto);
                if (result.Succeeded && userId.HasValue)
                {
                    var newAdminUser = await userManager.FindByIdAsync(userId.Value.ToString());
                    tenantId = newAdminUser?.TenantId;
                }
                else { Console.WriteLine($"Failed to register admin: {string.Join(", ", result.Errors.Select(e => e.Description))}"); return; }
            }
            else
            {
                tenantId = existingTenant?.Id ?? adminUser?.TenantId;
                if (adminUser != null && adminUser.TenantId == null && existingTenant != null)
                {
                    adminUser.TenantId = existingTenant.Id;
                    await userManager.UpdateAsync(adminUser);
                }
                else if (adminUser != null && existingTenant == null && adminUser.TenantId.HasValue)
                {
                    existingTenant = await context.Tenants.FindAsync(adminUser.TenantId.Value);
                    if (existingTenant == null) { Console.WriteLine($"Admin {adminUser.Email} has TenantId {adminUser.TenantId.Value} but tenant not found."); return; }
                    if (existingTenant.Subdomain != defaultSubdomain)
                    {
                        Console.WriteLine($"Warning: Admin {adminUser.Email} tied to tenant {existingTenant.Subdomain}, but seeding for {defaultSubdomain}. Using admin's tenant {existingTenant.Subdomain}.");
                        defaultSubdomain = existingTenant.Subdomain; defaultBusinessName = existingTenant.Name;
                    }
                    tenantId = adminUser.TenantId.Value;
                }
                if (!tenantId.HasValue) { Console.WriteLine("Could not determine tenant ID for seeding."); return; }
            }

            if (tenantId.HasValue)
            {
                var defaultCategoriesInput = new List<CreateCategoryDto>
                {
                    new CreateCategoryDto { Name = "Tortas Clasicas" }, new CreateCategoryDto { Name = "Masitas Secas" },
                    new CreateCategoryDto { Name = "Postres Frios" }, new CreateCategoryDto { Name = "Panadería Salada" },
                    new CreateCategoryDto { Name = "Bebidas" }, new CreateCategoryDto { Name = "Especialidades" },
                    new CreateCategoryDto { Name = "Tortas Premium" }, new CreateCategoryDto { Name = "Chocolatería" },
                    new CreateCategoryDto { Name = "Vegano" }
                };
                var createdCategoryDtos = new List<CategoryDto>();

                if (!await context.Categories.Where(c => c.TenantId == tenantId.Value).AnyAsync())
                {
                    foreach (var catDtoIn in defaultCategoriesInput)
                    {
                        var createdCatDto = await categoryService.CreateCategoryForAdminAsync(catDtoIn, tenantId.Value);
                        if (createdCatDto != null) createdCategoryDtos.Add(createdCatDto);
                        else
                        {
                            var existingCat = await context.Categories.IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value && EF.Functions.ILike(c.Name, catDtoIn.Name));
                            if (existingCat != null) createdCategoryDtos.Add(new CategoryDto { Id = existingCat.Id, Name = existingCat.Name });
                        }
                    }
                }
                else
                {
                    createdCategoryDtos = await context.Categories
                        .Where(c => c.TenantId == tenantId.Value && !c.IsDeleted)
                        .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
                        .ToListAsync();
                }
                if (!createdCategoryDtos.Any()) { Console.WriteLine("No categories available for product seeding for tenant " + tenantId.Value); }

                var categoryNameToIdMap = createdCategoryDtos.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

                var createdProductDtos = new List<ProductDto>();

                if (!await context.Products.Where(p => p.TenantId == tenantId.Value).AnyAsync())
                {
                    var defaultProductsInput = GenerateDefaultProducts(categoryNameToIdMap);
                    foreach (var prodDtoIn in defaultProductsInput)
                    {
                        if (prodDtoIn.CategoryId != Guid.Empty && categoryNameToIdMap.ContainsValue(prodDtoIn.CategoryId))
                        {
                            var createdProdDto = await productService.CreateProductForAdminAsync(prodDtoIn, tenantId.Value);
                            if (createdProdDto != null) createdProductDtos.Add(createdProdDto);
                        }
                    }
                }
                else
                {
                    createdProductDtos = (await productService.GetAllProductsForAdminAsync(tenantId.Value)).ToList();
                }



                var originalCustomerProfiles = new List<(string Name, List<string> PreferredCategoryNames)>
                {
                    ("Ana García", new List<string> { "Tortas Clasicas", "Masitas Secas" }),
                    ("Luis Rodríguez", new List<string> { "Panadería Salada", "Bebidas" }),
                    ("Carmen Fernández", new List<string> { "Postres Frios", "Especialidades" }),
                    ("Javier Martínez", new List<string> { "Tortas Premium", "Chocolatería" }),
                    ("Isabel Sánchez", new List<string> { "Vegano", "Bebidas" }),
                    ("Miguel González", new List<string> { "Masitas Secas", "Chocolatería" }),
                    ("Laura Jiménez", new List<string> { "Tortas Clasicas", "Postres Frios" }),
                    ("David Moreno", new List<string> { "Panadería Salada", "Especialidades" }),
                    ("Sofía Álvarez", new List<string> { "Tortas Premium", "Vegano" }),
                    ("Pedro Ramírez", new List<string> { "Bebidas", "Masitas Secas" }),
                    ("Elena Torres", new List<string> { "Chocolatería", "Especialidades" }),
                    ("Daniel Ruiz", new List<string> { "Tortas Clasicas" }),
                    ("Valeria Morales", new List<string> { "Vegano", "Postres Frios" }),
                    ("Carlos Ortega", new List<string> { "Tortas Premium" }),
                    ("Lucía Castillo", new List<string> { "Panadería Salada" })
                };

                string customerPasswordForSeeding = "qwe123QWE";
                var createdCustomerUsers = new List<ApplicationUser>();
                var customerSpecificPreferences = new Dictionary<Guid, List<Guid>>();


                var existingTenantMembers = await context.Users
                    .Where(u => u.TenantMemberships.Any(tm => tm.TenantId == tenantId.Value && tm.IsActive))
                    .Include(u => u.TenantMemberships).ToListAsync();
                createdCustomerUsers.AddRange(existingTenantMembers);

                foreach (var member in existingTenantMembers)
                {
                    var profileMatch = originalCustomerProfiles.FirstOrDefault(p => p.Name == member.Name);
                    if (profileMatch.Name != null && !customerSpecificPreferences.ContainsKey(member.Id))
                    {
                        customerSpecificPreferences[member.Id] = profileMatch.PreferredCategoryNames
                           .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                           .Where(id => id != Guid.Empty).ToList();
                    }
                }


                for (int i = 0; i < originalCustomerProfiles.Count; i++)
                {
                    var currentProfile = originalCustomerProfiles[i];
                    string customerName = currentProfile.Name;
                    string customerEmail = $"cliente{i + 1}@gmail.com";
                    string customerPhone = $"{random.Next(60000000, 79999999)}";

                    if (createdCustomerUsers.Any(u => u.Email == customerEmail)) continue;

                    var customerDto = new CustomerRegisterDto
                    {
                        Name = customerName,
                        Email = customerEmail,
                        Password = customerPasswordForSeeding,
                        ConfirmPassword = customerPasswordForSeeding,
                        PhoneNumber = customerPhone
                    };
                    var regResult = await accountService.RegisterCustomerForTenantAsync(customerDto, tenantId.Value);
                    if (regResult.IdentityResult.Succeeded && regResult.UserId.HasValue)
                    {
                        var appUser = await userManager.FindByIdAsync(regResult.UserId.Value.ToString());
                        if (appUser != null && !createdCustomerUsers.Contains(appUser))
                        {
                            createdCustomerUsers.Add(appUser);
                            customerSpecificPreferences[appUser.Id] = currentProfile.PreferredCategoryNames
                                .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                                .Where(id => id != Guid.Empty).ToList();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to register/link customer {customerEmail} for tenant {tenantId.Value}: {string.Join(", ", regResult.IdentityResult.Errors.Select(e => e.Description))}");
                    }
                }


                var enriqueProfile = ("Enrique Vera", new List<string> { "Tortas Clasicas", "Masitas Secas", "Tortas Premium" });
                string enriqueEmail = "enrique@gmail.com";
                string enriquePhone = "76469620";
                ApplicationUser? enriqueUserObject = createdCustomerUsers.FirstOrDefault(u => u.Email == enriqueEmail);

                if (enriqueUserObject == null)
                {
                    var enriqueDto = new CustomerRegisterDto
                    {
                        Name = enriqueProfile.Item1,
                        Email = enriqueEmail,
                        Password = customerPasswordForSeeding,
                        ConfirmPassword = customerPasswordForSeeding,
                        PhoneNumber = enriquePhone
                    };
                    var regResultEnrique = await accountService.RegisterCustomerForTenantAsync(enriqueDto, tenantId.Value);
                    if (regResultEnrique.IdentityResult.Succeeded && regResultEnrique.UserId.HasValue)
                    {
                        enriqueUserObject = await userManager.FindByIdAsync(regResultEnrique.UserId.Value.ToString());
                        if (enriqueUserObject != null && !createdCustomerUsers.Contains(enriqueUserObject))
                        {
                            createdCustomerUsers.Add(enriqueUserObject);
                            customerSpecificPreferences[enriqueUserObject.Id] = enriqueProfile.Item2
                               .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                               .Where(id => id != Guid.Empty).ToList();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to register/link Enrique Vera for tenant {tenantId.Value}: {string.Join(", ", regResultEnrique.IdentityResult.Errors.Select(e => e.Description))}");
                    }
                }
                else if (!customerSpecificPreferences.ContainsKey(enriqueUserObject.Id))
                {
                    customerSpecificPreferences[enriqueUserObject.Id] = enriqueProfile.Item2
                       .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                       .Where(id => id != Guid.Empty).ToList();
                }



                var ordersExistForTenant = await context.Orders.AnyAsync(o => o.TenantId == tenantId.Value);
                if (createdCustomerUsers.Any() && createdProductDtos.Any() && !ordersExistForTenant)
                {
                    var productsForOrders = createdProductDtos.Where(p => p.IsAvailable).ToList();
                    if (productsForOrders.Any())
                    {
                        foreach (var customerUser in createdCustomerUsers.Where(cu => cu.TenantMemberships.Any(tm => tm.TenantId == tenantId.Value)))
                        {
                            int numberOfOrdersForCustomer = (customerUser.Email == "enrique@gmail.com") ? 1 : random.Next(1, 3);

                            for (int i = 0; i < numberOfOrdersForCustomer; i++)
                            {
                                var orderItems = new List<OrderItem>();
                                decimal orderTotalAmount = 0m;
                                int numberOfProductsInOrder = random.Next(1, Math.Min(3, productsForOrders.Count + 1));
                                var chosenProductsForThisOrder = new HashSet<Guid>();

                                List<Guid> preferredCatsForUser = new List<Guid>();
                                if (customerSpecificPreferences.TryGetValue(customerUser.Id, out var prefs)) preferredCatsForUser = prefs;

                                if (preferredCatsForUser.Any())
                                {
                                    var preferredProds = productsForOrders.Where(p => preferredCatsForUser.Contains(p.CategoryId)).ToList();
                                    int takeFromPreferred = preferredProds.Any() ? random.Next(0, Math.Min(numberOfProductsInOrder, preferredProds.Count) + 1) : 0;

                                    foreach (var productToAdd in preferredProds.OrderBy(x => random.Next()).Take(takeFromPreferred))
                                    {
                                        if (chosenProductsForThisOrder.Add(productToAdd.Id))
                                        {
                                            int quantity = random.Next(1, 2);
                                            orderItems.Add(new OrderItem { Id = Guid.NewGuid(), ProductId = productToAdd.Id, ProductName = productToAdd.Name, Quantity = quantity, UnitPrice = productToAdd.Price });
                                            orderTotalAmount += productToAdd.Price * quantity;
                                        }
                                    }
                                }
                                int remainingProductsToChoose = numberOfProductsInOrder - orderItems.Count;
                                if (remainingProductsToChoose > 0)
                                {
                                    var otherProds = productsForOrders.Where(p => !chosenProductsForThisOrder.Contains(p.Id) && !preferredCatsForUser.Contains(p.CategoryId)).ToList();
                                    foreach (var productToAdd in otherProds.OrderBy(x => random.Next()).Take(remainingProductsToChoose))
                                    {
                                        if (chosenProductsForThisOrder.Add(productToAdd.Id))
                                        {
                                            int quantity = random.Next(1, 2);
                                            orderItems.Add(new OrderItem { Id = Guid.NewGuid(), ProductId = productToAdd.Id, ProductName = productToAdd.Name, Quantity = quantity, UnitPrice = productToAdd.Price });
                                            orderTotalAmount += productToAdd.Price * quantity;
                                        }
                                    }
                                }
                                if (!orderItems.Any()) continue;
                                var orderDate = DateTimeOffset.UtcNow.AddDays(-random.Next(7, 200));
                                var deliveryDate = orderDate.AddDays(random.Next(1, 7));
                                var orderStatus = GetRandomPastOrderStatus(random);
                                var updatedAt = orderStatus == OrderStatus.Pending ? orderDate : orderDate.AddMinutes(random.Next(5, 60 * 24));
                                await context.Orders.AddAsync(new Order { Id = Guid.NewGuid(), TenantId = tenantId.Value, ApplicationUserId = customerUser.Id, OrderDate = orderDate, DeliveryDate = deliveryDate, TotalAmount = orderTotalAmount, Status = orderStatus, CreatedAt = orderDate, UpdatedAt = updatedAt, OrderItems = orderItems });
                            }
                        }
                        await context.SaveChangesAsync();
                    }
                }


                ApplicationUser? enriqueUserForOrders = await userManager.FindByEmailAsync("enrique@gmail.com");
                if (enriqueUserForOrders != null && createdProductDtos.Any())
                {
                    bool isEnriqueMember = await context.CustomerTenantMemberships
                                               .AnyAsync(m => m.ApplicationUserId == enriqueUserForOrders.Id && m.TenantId == tenantId.Value && m.IsActive);
                    if (isEnriqueMember)
                    {
                        Guid tortasClasicasCatId = categoryNameToIdMap.TryGetValue("Tortas Clasicas", out var tcId) ? tcId : Guid.Empty;
                        Guid masitasSecasCatId = categoryNameToIdMap.TryGetValue("Masitas Secas", out var msId) ? msId : Guid.Empty;
                        Guid tortasPremiumCatId = categoryNameToIdMap.TryGetValue("Tortas Premium", out var tpId) ? tpId : Guid.Empty;

                        var tortasProducts = createdProductDtos.Where(p => (p.CategoryId == tortasClasicasCatId || p.CategoryId == tortasPremiumCatId) && p.IsAvailable).ToList();
                        var masitasProducts = createdProductDtos.Where(p => p.CategoryId == masitasSecasCatId && p.IsAvailable).ToList();
                        int enriqueOrderCount = 0;
                        for (int k = 0; k < 25; k++)
                        {

                            var orderItems = new List<OrderItem>(); decimal orderTotalAmount = 0m;
                            int numberOfProductsInOrder = random.Next(1, 5); var chosenProductsForThisOrder = new HashSet<Guid>();
                            int orderType = random.Next(0, 3); List<ProductDto> productsToChooseFrom = new List<ProductDto>();
                            if (orderType == 0 && tortasProducts.Any()) { productsToChooseFrom.AddRange(tortasProducts); }
                            else if (orderType == 1 && masitasProducts.Any()) { productsToChooseFrom.AddRange(masitasProducts); }
                            else
                            {
                                if (tortasProducts.Any()) productsToChooseFrom.AddRange(tortasProducts.OrderBy(x => random.Next()).Take(random.Next(1, Math.Max(2, tortasProducts.Count / 2 + 1))));
                                if (masitasProducts.Any()) productsToChooseFrom.AddRange(masitasProducts.OrderBy(x => random.Next()).Take(random.Next(1, Math.Max(2, masitasProducts.Count / 2 + 1))));
                            }
                            productsToChooseFrom = productsToChooseFrom.DistinctBy(p => p.Id).OrderBy(x => random.Next()).Take(numberOfProductsInOrder).ToList();
                            foreach (var productToAdd in productsToChooseFrom)
                            {
                                if (chosenProductsForThisOrder.Add(productToAdd.Id))
                                {
                                    int quantity = random.Next(1, 3);
                                    orderItems.Add(new OrderItem { Id = Guid.NewGuid(), ProductId = productToAdd.Id, ProductName = productToAdd.Name, Quantity = quantity, UnitPrice = productToAdd.Price });
                                    orderTotalAmount += productToAdd.Price * quantity;
                                }
                            }
                            if (!orderItems.Any()) continue;
                            var orderDate = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 730));
                            var deliveryDate = orderDate.AddDays(random.Next(0, 3));
                            var orderStatus = GetRandomPastOrderStatus(random);
                            var updatedAt = orderStatus == OrderStatus.Pending ? orderDate : orderDate.AddMinutes(random.Next(5, 60 * 24 * 2));
                            await context.Orders.AddAsync(new Order { Id = Guid.NewGuid(), TenantId = tenantId.Value, ApplicationUserId = enriqueUserForOrders.Id, OrderDate = orderDate, DeliveryDate = deliveryDate, TotalAmount = orderTotalAmount, Status = orderStatus, CreatedAt = orderDate, UpdatedAt = updatedAt, OrderItems = orderItems });
                            enriqueOrderCount++;
                        }
                        if (enriqueOrderCount > 0) { await context.SaveChangesAsync(); Console.WriteLine($"Seeded {enriqueOrderCount} specific orders for Enrique Vera for tenant {tenantId.Value}."); }
                    }
                    else { Console.WriteLine($"Enrique Vera (enrique@gmail.com) is not an active member of tenant {tenantId.Value}. Skipping specific order seeding."); }
                }
                else if (enriqueUserForOrders == null) { Console.WriteLine("Enrique Vera (enrique@gmail.com) not found. Cannot seed specific orders."); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ocurrió un error durante la inicialización de la BD: {ex.Message}");
            Console.WriteLine(ex.ToString());
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<ApplicationRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }

    private static OrderStatus GetRandomPastOrderStatus(Random random)
    {
        int randValue = random.Next(100);
        if (randValue < 75) return OrderStatus.Received;
        if (randValue < 85) return OrderStatus.Confirmed;
        if (randValue < 92) return OrderStatus.Ready;
        if (randValue < 98) return OrderStatus.Cancelled;
        return OrderStatus.Preparing;
    }


    private static List<CreateProductDto> GenerateDefaultProducts(Dictionary<string, Guid> categoryNameOrIdMap)
    {
        var products = new List<CreateProductDto>();
        var random = new Random();
        var allPossibleTags = new List<string> {
            "Sin Gluten", "Chocolate Intenso", "Frutal", "Para Niños", "Económico",
            "Cumpleaños", "Aniversario", "Postre Ligero", "Tradicional", "Artesanal",
            "Para Regalar", "Edición Limitada", "Keto Friendly", "Saludable"
        };
        List<string> GetRandomTags(int maxTags = 3)
        {
            if (random.Next(0, 4) == 0) return new List<string>();
            return allPossibleTags.OrderBy(x => random.Next()).Take(random.Next(1, maxTags + 1)).ToList();
        }
        Guid GetCatId(string name) => categoryNameOrIdMap.TryGetValue(name, out var id) ? id : Guid.Empty;

        Guid tortasClasicasId = GetCatId("Tortas Clasicas"); Guid masitasId = GetCatId("Masitas Secas");
        Guid postresFriosId = GetCatId("Postres Frios"); Guid saladosId = GetCatId("Panadería Salada");
        Guid bebidasId = GetCatId("Bebidas"); Guid especialesId = GetCatId("Especialidades");
        Guid tortasPremiumId = GetCatId("Tortas Premium"); Guid chocolateriaId = GetCatId("Chocolatería");
        Guid veganoId = GetCatId("Vegano");


        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta de Vainilla Celestial", Description = "Un clásico que enamora: bizcocho de vainilla increíblemente esponjoso y húmedo, cubierto con nuestro suave merengue italiano. ¡Perfecta para cualquier celebración!", Price = 75.00m, CategoryId = tortasClasicasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687684/bakery/products/vafswod6t8ilbdsjh2p0.jpg" }, LeadTimeInput = "2", Tags = new List<string> { "Cumpleaños", "Tradicional" } });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Alfajor de Maicena Artesanal (Uni.)", Description = "Delicadas tapitas de maicena que se deshacen en tu boca, unidas por un generoso corazón de dulce de leche premium y bordeado con coco rallado fresco.", Price = 4.50m, CategoryId = masitasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687755/bakery/products/ndw3uet0eop3lhltjsuj.jpg" }, Tags = new List<string> { "Tradicional", "Artesanal", "Económico" } });
        if (postresFriosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Mousse Exótico de Maracuyá", Description = "Una explosión de sabor tropical. Nuestro mousse ligero y aireado de maracuyá natural, sobre una base crocante de galleta. ¡Pura frescura!", Price = 15.00m, CategoryId = postresFriosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687818/bakery/products/lxjpibvwuhbftndbqr5r.jpg" }, LeadTimeInput = "1", Tags = new List<string> { "Frutal", "Postre Ligero" } });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Empanada de Pollo Casera", Description = "Jugoso relleno de pollo desmenuzado, sazonado con especias secretas de la casa, aceitunas y huevo, envuelto en una masa crujiente horneada a la perfección.", Price = 7.00m, CategoryId = saladosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687905/bakery/products/ndj3v7fshiyknjeafsib.jpg" }, Tags = GetRandomTags(2) });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Café Americano de Origen", Description = "Disfruta la pureza de un café de grano seleccionado, recién molido y preparado al momento para despertar tus sentidos con su aroma y sabor intenso.", Price = 10.00m, CategoryId = bebidasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687948/bakery/products/vmakiexrrayi4e6cdoyz.jpg" } });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto { Name = "Cheesecake Cremoso de Frutos Rojos", Description = "Irresistible cheesecake horneado al estilo New York, con una base de galleta artesanal, un relleno increíblemente cremoso y coronado con una vibrante salsa de frutos rojos naturales.", Price = 25.00m, CategoryId = especialesId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687987/bakery/products/ynamjhgtmuuyvj9s5cvd.jpg" }, LeadTimeInput = "2", Tags = new List<string> { "Frutal", "Para Regalar" } });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Ópera Clásica Francesa", Description = "Una obra maestra de la pastelería: finas capas de bizcocho de almendras bañadas en café, ganache de chocolate oscuro y crema de mantequilla de café. Elegancia pura.", Price = 150.00m, CategoryId = tortasPremiumId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688029/bakery/products/dhid4h5aojhuryki90js.jpg" }, LeadTimeInput = "3", Tags = new List<string> { "Chocolate Intenso", "Aniversario", "Artesanal" } });
        if (chocolateriaId != Guid.Empty) products.Add(new CreateProductDto { Name = "Tableta de Chocolate Amargo 70% Cacao", Description = "Para los amantes del chocolate intenso. Nuestra tableta artesanal elaborada con cacao de origen al 70%, con notas frutales y un final persistente.", Price = 30.00m, CategoryId = chocolateriaId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688066/bakery/products/qppydcnx73rquqzczshw.jpg" }, Tags = new List<string> { "Chocolate Intenso", "Artesanal" } });
        if (chocolateriaId != Guid.Empty) products.Add(new CreateProductDto { Name = "Caja de Bombones Artesanales Surtidos (12u)", Description = "Descubre una exquisita selección de 12 bombones hechos a mano con los mejores ingredientes y rellenos sorprendentes. Ideal para regalar o darte un capricho.", Price = 60.00m, CategoryId = chocolateriaId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688098/bakery/products/rr4xrxdli2nw9sqj2swj.jpg" }, LeadTimeInput = "1", Tags = GetRandomTags() });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto { Name = "Brownie Vegano Intenso con Nueces", Description = "Un placer chocolatoso sin culpas. Brownie 100% vegano, denso, húmedo y cargado de trozos de nuez crujiente. ¡Te sorprenderá!", Price = 18.00m, CategoryId = veganoId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688140/bakery/products/gy1y1tsjaelnmekikarh.jpg" }, Tags = new List<string> { "Vegano", "Chocolate Intenso", "Saludable" } });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto { Name = "Galletas Veganas de Avena y Pasas (6u)", Description = "Tiernas por dentro, crujientes por fuera. Galletas veganas hechas con avena integral, jugosas pasas y un toque de canela. Perfectas para tu snack.", Price = 20.00m, CategoryId = veganoId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688172/bakery/products/da9fsg7hgndvw51pqk07.jpg" }, Tags = new List<string> { "Vegano", "Saludable" } });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Docena de Mini Quiches Lorraine", Description = "Bocados perfectos para cualquier evento. Nuestras clásicas mini quiches con panceta, queso y una cremosa mezcla de huevo y nata, en masa quebrada.", Price = 50.00m, CategoryId = saladosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688247/bakery/products/nkqkuojc3nqzozhrwkkn.jpg" }, LeadTimeInput = "1", Tags = GetRandomTags(1) });
        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Tres Leches Tradicional", Description = "Un postre legendario. Bizcocho esponjoso generosamente bañado en una mezcla de tres leches, coronado con merengue y un toque de canela. ¡Pura delicia!", Price = 90.00m, CategoryId = tortasClasicasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688366/bakery/products/nr9ibugvjt8klvvxymkb.jpg" }, LeadTimeInput = "2", Tags = new List<string> { "Tradicional", "Cumpleaños" } });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Palmeritas Crocantes de Hojaldre (200g)", Description = "Imposible comer solo una. Ligeras y crujientes palmeritas de hojaldre caramelizado, perfectas para acompañar tu café o té.", Price = 22.00m, CategoryId = masitasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688406/bakery/products/dskrjypvhjfhlpi8fezk.jpg" }, Tags = new List<string> { "Artesanal", "Económico" } });
        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Selva Negra Auténtica", Description = "Un viaje a Alemania en cada bocado. Capas de bizcocho de chocolate embebido en kirsch, crema chantilly fresca y abundantes cerezas.", Price = 110.00m, CategoryId = tortasClasicasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689546/bakery/products/z3ilnvulloryqnopzxql.jpg" }, LeadTimeInput = "2", Tags = GetRandomTags() });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Caja de Galletas Surtidas Premium", Description = "Una fina selección de nuestras mejores galletas artesanales: de mantequilla, con chips de chocolate, decoradas... ¡Un festín para los sentidos!", Price = 45.00m, CategoryId = masitasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688523/bakery/products/mkwrvvb3k9cepvehibqa.jpg" }, Tags = new List<string> { "Para Regalar", "Artesanal" } });
        if (postresFriosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Tiramisú Italiano Clásico", Description = "La receta tradicional italiana. Suaves bizcochos de soletilla bañados en café espresso, crema de mascarpone y un toque de cacao amargo. ¡Delizioso!", Price = 22.00m, CategoryId = postresFriosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688551/bakery/products/dv8lae6q8z9pludhbapy.jpg" }, LeadTimeInput = "1", Tags = new List<string> { "Tradicional", "Postre Ligero" } });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Cuñapé Horneado Tradicional (Uni.)", Description = "El sabor de nuestra tierra. Delicioso pan de queso almidón de yuca y queso fresco, horneado hasta quedar dorado y esponjoso.", Price = 5.00m, CategoryId = saladosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688684/bakery/products/lm8lkk37rrb78zetxgpz.jpg" }, Tags = new List<string> { "Tradicional", "Económico", "Sin Gluten" } });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Chocolate Caliente Espeso de la Abuela", Description = "Como el de antes. Chocolate caliente cremoso y reconfortante, perfecto para los días fríos o para darte un gusto especial. ¡Puro placer!", Price = 18.00m, CategoryId = bebidasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688763/bakery/products/ikpbhfdwpedbcdq8plij.jpg" }, Tags = GetRandomTags(1) });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto { Name = "Volcán de Chocolate Fundido", Description = "Una erupción de sabor. Bizcocho de chocolate intenso con un corazón líquido de chocolate fundido que se derrama al primer corte. Servir tibio.", Price = 28.00m, CategoryId = especialesId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688836/bakery/products/rzwgp4wwfajenk3k4cuc.jpg" }, LeadTimeInput = "1", Tags = new List<string> { "Chocolate Intenso", "Postre Ligero" } });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Red Velvet Aterciopelada", Description = "Un ícono americano. Bizcocho de un rojo intenso y textura aterciopelada, con un sutil sabor a cacao, relleno y cubierto con frosting de queso crema.", Price = 130.00m, CategoryId = tortasPremiumId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688862/bakery/products/pwsdjyjh26eod13ifcoc.jpg" }, LeadTimeInput = "3", Tags = new List<string> { "Cumpleaños", "Aniversario", "Edición Limitada" } });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto { Name = "Empanada de Carne Criolla", Description = "Sabor auténtico. Tierno relleno de carne de res cortada a cuchillo, cocinada lentamente con cebolla, pimentón y especias, en una masa casera.", Price = 7.50m, CategoryId = saladosId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688894/bakery/products/rbmwmoac9tc2udjearyk.jpg" }, Tags = GetRandomTags(2) });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Rollo de Canela Glaseado Individual", Description = "Aroma que enamora. Rollo de canela esponjoso y tierno, relleno de azúcar moreno y canela, cubierto con un delicioso glaseado de queso crema.", Price = 12.00m, CategoryId = masitasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688960/bakery/products/x60fsk4c9tgpjkktxyq7.jpg" }, Tags = GetRandomTags(1) });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto { Name = "Jugo Natural de Naranja Recién Exprimido", Description = "Vitamina C pura y refrescante. Jugo 100% natural de naranjas frescas, exprimido al momento para conservar todo su sabor y propiedades.", Price = 15.00m, CategoryId = bebidasId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688986/bakery/products/cgz3rjcr9hyurgoyijs5.jpg" }, Tags = new List<string> { "Frutal", "Saludable" } });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto { Name = "Tarta de Manzana Vegana Rústica", Description = "Un clásico reinventado. Delicadas manzanas caramelizadas sobre una base de masa quebrada vegana, con un toque de canela. Simple y exquisita.", Price = 95.00m, CategoryId = veganoId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689060/bakery/products/e55fbzjvrhsz4j7s7wxh.jpg" }, LeadTimeInput = "4", Tags = new List<string> { "Vegano", "Frutal", "Artesanal" } });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto { Name = "Barra Energética Vegana de Frutos Secos", Description = "El snack saludable perfecto. Combinación de dátiles, nueces, almendras, semillas de chía y un toque de cacao puro. Energía natural sin aditivos.", Price = 10.00m, CategoryId = veganoId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689144/bakery/products/felrfvy6jh9c9rl3rgih.jpg" }, Tags = new List<string> { "Vegano", "Saludable", "Económico" } });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Keto de Almendras y Chocolate", Description = "Deliciosa torta baja en carbohidratos, hecha con harina de almendras, chocolate oscuro sin azúcar y endulzantes naturales. Perfecta para dietas keto.", Price = 120.00m, CategoryId = especialesId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1749087425/bakery/products/ktnevbj2ilepksfyykig.jpg" }, LeadTimeInput = "3", Tags = new List<string> { "Keto Friendly", "Chocolate Intenso", "Sin Gluten", "Saludable" } });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto { Name = "Torta Unicornio Mágico para Niños", Description = "Una torta de ensueño para los más pequeños. Bizcocho de colores, relleno de crema de vainilla y decorada con temática de unicornio. Ideal para cumpleaños infantiles.", Price = 180.00m, CategoryId = tortasPremiumId, Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1749087527/bakery/products/uykudoekeiku8bxsjq85.jpg", }, LeadTimeInput = "4", Tags = new List<string> { "Para Niños", "Cumpleaños", "Edición Limitada", "Frutal" } });

        return products;
    }
}