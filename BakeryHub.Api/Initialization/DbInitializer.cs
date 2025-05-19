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

            string defaultAdminEmail = "admin@admin.com";
            string defaultPassword = "qwe123QWE";
            string defaultSubdomain = "pasteleria";
            string defaultBusinessName = "Sabor a Hogar";

            Guid? tenantId = null;
            ApplicationUser? adminUser = await userManager.FindByEmailAsync(defaultAdminEmail);
            Tenant? existingTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == defaultSubdomain);

            if (adminUser == null && existingTenant == null)
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
                }
                else
                {
                    return;
                }
            }
            else
            {
                tenantId = existingTenant?.Id ?? adminUser?.TenantId;
                if (!tenantId.HasValue) return;
            }

            if (tenantId.HasValue)
            {
                var defaultCategoriesInput = new List<CreateCategoryDto>
                {
                    new CreateCategoryDto { Name = "Tortas Clasicas" },
                    new CreateCategoryDto { Name = "Masitas Secas" },
                    new CreateCategoryDto { Name = "Postres Frios" },
                    new CreateCategoryDto { Name = "Panadería Salada" },
                    new CreateCategoryDto { Name = "Bebidas" },
                    new CreateCategoryDto { Name = "Especialidades" },
                    new CreateCategoryDto { Name = "Tortas Premium" },
                    new CreateCategoryDto { Name = "Chocolatería" },
                    new CreateCategoryDto { Name = "Vegano" }
                };
                var createdCategoryDtos = new List<CategoryDto>();
                if (!await context.Categories.Where(c => c.TenantId == tenantId.Value).AnyAsync())
                {
                    foreach (var catDtoIn in defaultCategoriesInput)
                    {
                        var createdCatDto = await categoryService.CreateCategoryForAdminAsync(catDtoIn, tenantId.Value);
                        if (createdCatDto != null)
                            createdCategoryDtos.Add(createdCatDto);
                        else
                        {
                            var existingCat = await context.Categories.IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value && EF.Functions.ILike(c.Name, catDtoIn.Name));
                            if (existingCat != null)
                                createdCategoryDtos.Add(new CategoryDto { Id = existingCat.Id, Name = existingCat.Name });
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
                if (!createdCategoryDtos.Any()) return;

                var categoryNameToIdMap = createdCategoryDtos.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

                var createdProductDtos = new List<ProductDto>();
                if (!await context.Products.Where(p => p.TenantId == tenantId.Value).AnyAsync())
                {
                    var defaultProductsInput = GenerateDefaultProducts(categoryNameToIdMap);
                    foreach (var prodDtoIn in defaultProductsInput)
                    {
                        if (prodDtoIn.CategoryId != Guid.Empty && createdCategoryDtos.Any(c => c.Id == prodDtoIn.CategoryId))
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
                if (!createdProductDtos.Any()) return;

                var customerProfiles = new List<(string Name, List<string> PreferredCategoryNames)>
                {
                    ("Ana García", new List<string> { "Tortas Clasicas", "Masitas Secas" }),
                    ("Luis Rodríguez", new List<string> { "Panadería Salada", "Bebidas Calientes" }),
                    ("Carmen Fernández", new List<string> { "Postres Frios", "Especialidades" }),
                    ("Javier Martínez", new List<string> { "Tortas Premium", "Chocolatería" }),
                    ("Isabel Sánchez", new List<string> { "Vegano", "Bebidas Calientes" }),
                    ("Miguel González", new List<string> { "Masitas Secas", "Chocolatería" }),
                    ("Laura Jiménez", new List<string> { "Tortas Clasicas", "Postres Frios" }),
                    ("David Moreno", new List<string> { "Panadería Salada", "Especialidades" }),
                    ("Sofía Álvarez", new List<string> { "Tortas Premium", "Vegano" }),
                    ("Pedro Ramírez", new List<string> { "Bebidas Calientes", "Masitas Secas" }),
                    ("Elena Torres", new List<string> { "Chocolatería", "Especialidades" }),
                    ("Daniel Ruiz", new List<string> { "Tortas Clasicas" }),
                    ("Valeria Morales", new List<string> { "Vegano", "Postres Frios" }),
                    ("Carlos Ortega", new List<string> { "Tortas Premium" }),
                    ("Lucía Castillo", new List<string> { "Panadería Salada" })
                };

                int numberOfCustomersToCreate = customerProfiles.Count;
                string customerPasswordForSeeding = "qwe123QWE";
                var createdCustomerUsers = new List<ApplicationUser>();
                var customerSpecificPreferences = new Dictionary<Guid, List<Guid>>();

                var existingCustomersForTenant = await context.Users
                    .Where(u => u.TenantMemberships.Any(tm => tm.TenantId == tenantId.Value))
                    .Include(u => u.TenantMemberships)
                    .ToListAsync();
                createdCustomerUsers.AddRange(existingCustomersForTenant);

                foreach (var existingCust in existingCustomersForTenant)
                {
                    var profile = customerProfiles.FirstOrDefault(p => p.Name == existingCust.Name);
                    if (profile.Name != null)
                    {
                        customerSpecificPreferences[existingCust.Id] = profile.PreferredCategoryNames
                            .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                            .Where(id => id != Guid.Empty)
                            .ToList();
                    }
                }

                if (createdCustomerUsers.Count < numberOfCustomersToCreate)
                {
                    for (int i = 0; i < numberOfCustomersToCreate; i++)
                    {
                        if (i >= customerProfiles.Count) break;
                        var currentProfile = customerProfiles[i];
                        string customerName = currentProfile.Name;
                        string customerEmail = $"cliente{i + 1}@gmail.com";

                        if (createdCustomerUsers.Any(u => u.Email == customerEmail || u.Name == customerName))
                        {
                            var existingUserInList = createdCustomerUsers.First(u => u.Email == customerEmail || u.Name == customerName);
                            if (!customerSpecificPreferences.ContainsKey(existingUserInList.Id))
                            {
                                customerSpecificPreferences[existingUserInList.Id] = currentProfile.PreferredCategoryNames
                                    .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                                    .Where(id => id != Guid.Empty)
                                    .ToList();
                            }
                            continue;
                        }

                        string customerPhone = $"{random.Next(60000000, 79999999)}";
                        var existingUserForEmail = await userManager.FindByEmailAsync(customerEmail);

                        if (existingUserForEmail != null)
                        {
                            bool isAlreadyMember = existingUserForEmail.TenantMemberships.Any(tm => tm.TenantId == tenantId.Value);
                            if (!isAlreadyMember)
                            {
                                context.CustomerTenantMemberships.Add(new CustomerTenantMembership
                                {
                                    ApplicationUserId = existingUserForEmail.Id,
                                    TenantId = tenantId.Value,
                                    IsActive = true,
                                    DateJoined = DateTimeOffset.UtcNow
                                });
                            }
                            if (!createdCustomerUsers.Any(u => u.Id == existingUserForEmail.Id))
                            {
                                createdCustomerUsers.Add(existingUserForEmail);
                            }
                            customerSpecificPreferences[existingUserForEmail.Id] = currentProfile.PreferredCategoryNames
                                .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                                .Where(id => id != Guid.Empty)
                                .ToList();
                            continue;
                        }

                        var customerDto = new CustomerRegisterDto
                        {
                            Name = customerName,
                            Email = customerEmail,
                            Password = customerPasswordForSeeding,
                            ConfirmPassword = customerPasswordForSeeding,
                            PhoneNumber = customerPhone
                        };

                        var detailedRegistrationOutcome = await accountService.RegisterCustomerForTenantAsync(customerDto, tenantId.Value);
                        if (detailedRegistrationOutcome.IdentityResult.Succeeded && detailedRegistrationOutcome.UserId.HasValue)
                        {
                            var appUser = await userManager.FindByIdAsync(detailedRegistrationOutcome.UserId.Value.ToString());
                            if (appUser != null && !createdCustomerUsers.Any(u => u.Id == appUser.Id))
                            {
                                createdCustomerUsers.Add(appUser);
                                customerSpecificPreferences[appUser.Id] = currentProfile.PreferredCategoryNames
                                    .Select(name => categoryNameToIdMap.TryGetValue(name, out var catId) ? catId : Guid.Empty)
                                    .Where(id => id != Guid.Empty)
                                    .ToList();
                            }
                        }
                        await Task.Delay(30);
                    }
                    await context.SaveChangesAsync();
                }

                var ordersExistForTenant = await context.Orders.AnyAsync(o => o.TenantId == tenantId.Value);
                if (createdCustomerUsers.Any() && createdProductDtos.Any() && !ordersExistForTenant)
                {
                    var productsForOrders = createdProductDtos.Where(p => p.IsAvailable).ToList();
                    if (!productsForOrders.Any()) return;

                    foreach (var customerUser in createdCustomerUsers)
                    {
                        int numberOfOrdersForCustomer = random.Next(2, 6);
                        for (int i = 0; i < numberOfOrdersForCustomer; i++)
                        {
                            var orderItems = new List<OrderItem>();
                            decimal orderTotalAmount = 0m;
                            int numberOfProductsInOrder = random.Next(1, Math.Min(6, productsForOrders.Count + 1));
                            var chosenProductsForThisOrder = new HashSet<Guid>();

                            if (customerSpecificPreferences.TryGetValue(customerUser.Id, out var preferredCats) && preferredCats.Any())
                            {
                                var preferredProds = productsForOrders.Where(p => preferredCats.Contains(p.CategoryId)).ToList();
                                int takeFromPreferred = random.Next(Math.Min(1, preferredProds.Count), Math.Min(numberOfProductsInOrder, preferredProds.Count) + 1);
                                foreach (var productToAdd in preferredProds.OrderBy(x => random.Next()).Take(takeFromPreferred))
                                {
                                    if (chosenProductsForThisOrder.Add(productToAdd.Id))
                                    {
                                        int quantity = random.Next(1, 3);
                                        orderItems.Add(new OrderItem
                                        {
                                            Id = Guid.NewGuid(),
                                            ProductId = productToAdd.Id,
                                            ProductName = productToAdd.Name,
                                            Quantity = quantity,
                                            UnitPrice = productToAdd.Price
                                        });
                                        orderTotalAmount += productToAdd.Price * quantity;
                                    }
                                }
                            }

                            int remainingProductsToChoose = numberOfProductsInOrder - orderItems.Count;
                            if (remainingProductsToChoose > 0)
                            {
                                var otherProds = productsForOrders
                                    .Where(p => !chosenProductsForThisOrder.Contains(p.Id) && (preferredCats == null || !preferredCats.Contains(p.CategoryId)))
                                    .ToList();
                                foreach (var productToAdd in otherProds.OrderBy(x => random.Next()).Take(remainingProductsToChoose))
                                {
                                    if (chosenProductsForThisOrder.Add(productToAdd.Id))
                                    {
                                        int quantity = random.Next(1, 3);
                                        orderItems.Add(new OrderItem
                                        {
                                            Id = Guid.NewGuid(),
                                            ProductId = productToAdd.Id,
                                            ProductName = productToAdd.Name,
                                            Quantity = quantity,
                                            UnitPrice = productToAdd.Price
                                        });
                                        orderTotalAmount += productToAdd.Price * quantity;
                                    }
                                }
                            }

                            if (!orderItems.Any()) continue;

                            var orderDate = DateTimeOffset.UtcNow.AddDays(-random.Next(7, 200));
                            var deliveryDate = orderDate.AddDays(random.Next(1, 7));
                            var orderStatus = GetRandomPastOrderStatus(random);
                            var updatedAt = orderStatus == OrderStatus.Pending ? orderDate : orderDate.AddMinutes(random.Next(5, 60 * 24));

                            var orderEntity = new Order
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId.Value,
                                ApplicationUserId = customerUser.Id,
                                OrderDate = orderDate,
                                DeliveryDate = deliveryDate,
                                TotalAmount = orderTotalAmount,
                                Status = orderStatus,
                                CreatedAt = orderDate,
                                UpdatedAt = updatedAt,
                                OrderItems = orderItems
                            };
                            await context.Orders.AddAsync(orderEntity);
                        }
                    }
                    await context.SaveChangesAsync();
                }
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
        var statuses = new[] { OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Received, OrderStatus.Cancelled };
        int randValue = random.Next(100);
        if (randValue < 60) return OrderStatus.Received;
        if (randValue < 75) return OrderStatus.Confirmed;
        if (randValue < 85) return OrderStatus.Ready;
        if (randValue < 95) return OrderStatus.Cancelled;
        return OrderStatus.Preparing;
    }

    private static List<CreateProductDto> GenerateDefaultProducts(Dictionary<string, Guid> categoryNameOrIdMap)
    {
        var products = new List<CreateProductDto>();

        Guid GetCatId(string name) => categoryNameOrIdMap.TryGetValue(name, out var id) ? id : Guid.Empty;

        Guid tortasClasicasId = GetCatId("Tortas Clasicas");
        Guid masitasId = GetCatId("Masitas Secas");
        Guid postresFriosId = GetCatId("Postres Frios");
        Guid saladosId = GetCatId("Panadería Salada");
        Guid bebidasId = GetCatId("Bebidas Calientes");
        Guid especialesId = GetCatId("Especialidades");
        Guid tortasPremiumId = GetCatId("Tortas Premium");
        Guid chocolateriaId = GetCatId("Chocolatería");
        Guid veganoId = GetCatId("Vegano");


        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Torta de Vainilla Celestial",
            Description = "Un clásico que enamora: bizcocho de vainilla increíblemente esponjoso y húmedo, cubierto con nuestro suave merengue italiano. ¡Perfecta para cualquier celebración!",
            Price = 75.00m,
            CategoryId = tortasClasicasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687684/bakery/products/vafswod6t8ilbdsjh2p0.jpg" },
            LeadTimeInput = "2"
        });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Alfajor de Maicena Artesanal (Uni.)",
            Description = "Delicadas tapitas de maicena que se deshacen en tu boca, unidas por un generoso corazón de dulce de leche premium y bordeado con coco rallado fresco.",
            Price = 4.50m,
            CategoryId = masitasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687755/bakery/products/ndw3uet0eop3lhltjsuj.jpg" }
        });
        if (postresFriosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Mousse Exótico de Maracuyá",
            Description = "Una explosión de sabor tropical. Nuestro mousse ligero y aireado de maracuyá natural, sobre una base crocante de galleta. ¡Pura frescura!",
            Price = 15.00m,
            CategoryId = postresFriosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687818/bakery/products/lxjpibvwuhbftndbqr5r.jpg" },
            LeadTimeInput = "1"
        });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Empanada de Pollo Casera",
            Description = "Jugoso relleno de pollo desmenuzado, sazonado con especias secretas de la casa, aceitunas y huevo, envuelto en una masa crujiente horneada a la perfección.",
            Price = 7.00m,
            CategoryId = saladosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687905/bakery/products/ndj3v7fshiyknjeafsib.jpg" }
        });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Café Americano de Origen",
            Description = "Disfruta la pureza de un café de grano seleccionado, recién molido y preparado al momento para despertar tus sentidos con su aroma y sabor intenso.",
            Price = 10.00m,
            CategoryId = bebidasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687948/bakery/products/vmakiexrrayi4e6cdoyz.jpg" }
        });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Cheesecake Cremoso de Frutos Rojos",
            Description = "Irresistible cheesecake horneado al estilo New York, con una base de galleta artesanal, un relleno increíblemente cremoso y coronado con una vibrante salsa de frutos rojos naturales.",
            Price = 25.00m,
            CategoryId = especialesId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747687987/bakery/products/ynamjhgtmuuyvj9s5cvd.jpg" },
            LeadTimeInput = "2"
        });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Torta Ópera Clásica Francesa",
            Description = "Una obra maestra de la pastelería: finas capas de bizcocho de almendras bañadas en café, ganache de chocolate oscuro y crema de mantequilla de café. Elegancia pura.",
            Price = 150.00m,
            CategoryId = tortasPremiumId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688029/bakery/products/dhid4h5aojhuryki90js.jpg" },
            LeadTimeInput = "3"
        });
        if (chocolateriaId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Tableta de Chocolate Amargo 70% Cacao",
            Description = "Para los amantes del chocolate intenso. Nuestra tableta artesanal elaborada con cacao de origen al 70%, con notas frutales y un final persistente.",
            Price = 30.00m,
            CategoryId = chocolateriaId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688066/bakery/products/qppydcnx73rquqzczshw.jpg" }
        });
        if (chocolateriaId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Caja de Bombones Artesanales Surtidos (12u)",
            Description = "Descubre una exquisita selección de 12 bombones hechos a mano con los mejores ingredientes y rellenos sorprendentes. Ideal para regalar o darte un capricho.",
            Price = 60.00m,
            CategoryId = chocolateriaId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688098/bakery/products/rr4xrxdli2nw9sqj2swj.jpg" },
            LeadTimeInput = "1"
        });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Brownie Vegano Intenso con Nueces",
            Description = "Un placer chocolatoso sin culpas. Brownie 100% vegano, denso, húmedo y cargado de trozos de nuez crujiente. ¡Te sorprenderá!",
            Price = 18.00m,
            CategoryId = veganoId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688140/bakery/products/gy1y1tsjaelnmekikarh.jpg" }
        });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Galletas Veganas de Avena y Pasas (6u)",
            Description = "Tiernas por dentro, crujientes por fuera. Galletas veganas hechas con avena integral, jugosas pasas y un toque de canela. Perfectas para tu snack.",
            Price = 20.00m,
            CategoryId = veganoId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688172/bakery/products/da9fsg7hgndvw51pqk07.jpg" }
        });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Docena de Mini Quiches Lorraine",
            Description = "Bocados perfectos para cualquier evento. Nuestras clásicas mini quiches con panceta, queso y una cremosa mezcla de huevo y nata, en masa quebrada.",
            Price = 50.00m,
            CategoryId = saladosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688247/bakery/products/nkqkuojc3nqzozhrwkkn.jpg" },
            LeadTimeInput = "1"
        });
        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Torta Tres Leches Tradicional",
            Description = "Un postre legendario. Bizcocho esponjoso generosamente bañado en una mezcla de tres leches, coronado con merengue y un toque de canela. ¡Pura delicia!",
            Price = 90.00m,
            CategoryId = tortasClasicasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688366/bakery/products/nr9ibugvjt8klvvxymkb.jpg" },
            LeadTimeInput = "2"
        });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Palmeritas Crocantes de Hojaldre (200g)",
            Description = "Imposible comer solo una. Ligeras y crujientes palmeritas de hojaldre caramelizado, perfectas para acompañar tu café o té.",
            Price = 22.00m,
            CategoryId = masitasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688406/bakery/products/dskrjypvhjfhlpi8fezk.jpg" }
        });
        if (tortasClasicasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Torta Selva Negra Auténtica",
            Description = "Un viaje a Alemania en cada bocado. Capas de bizcocho de chocolate embebido en kirsch, crema chantilly fresca y abundantes cerezas.",
            Price = 110.00m,
            CategoryId = tortasClasicasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689546/bakery/products/z3ilnvulloryqnopzxql.jpg" },
            LeadTimeInput = "2"
        });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Caja de Galletas Surtidas Premium",
            Description = "Una fina selección de nuestras mejores galletas artesanales: de mantequilla, con chips de chocolate, decoradas... ¡Un festín para los sentidos!",
            Price = 45.00m,
            CategoryId = masitasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688523/bakery/products/mkwrvvb3k9cepvehibqa.jpg" }
        });
        if (postresFriosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Tiramisú Italiano Clásico",
            Description = "La receta tradicional italiana. Suaves bizcochos de soletilla bañados en café espresso, crema de mascarpone y un toque de cacao amargo. ¡Delizioso!",
            Price = 22.00m,
            CategoryId = postresFriosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688551/bakery/products/dv8lae6q8z9pludhbapy.jpg" },
            LeadTimeInput = "1"
        });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Cuñapé Horneado Tradicional (Uni.)",
            Description = "El sabor de nuestra tierra. Delicioso pan de queso almidón de yuca y queso fresco, horneado hasta quedar dorado y esponjoso.",
            Price = 5.00m,
            CategoryId = saladosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688684/bakery/products/lm8lkk37rrb78zetxgpz.jpg" }
        });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Chocolate Caliente Espeso de la Abuela",
            Description = "Como el de antes. Chocolate caliente cremoso y reconfortante, perfecto para los días fríos o para darte un gusto especial. ¡Puro placer!",
            Price = 18.00m,
            CategoryId = bebidasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688763/bakery/products/ikpbhfdwpedbcdq8plij.jpg" }
        });
        if (especialesId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Volcán de Chocolate Fundido",
            Description = "Una erupción de sabor. Bizcocho de chocolate intenso con un corazón líquido de chocolate fundido que se derrama al primer corte. Servir tibio.",
            Price = 28.00m,
            CategoryId = especialesId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688836/bakery/products/rzwgp4wwfajenk3k4cuc.jpg" },
            LeadTimeInput = "1"
        });
        if (tortasPremiumId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Torta Red Velvet Aterciopelada",
            Description = "Un ícono americano. Bizcocho de un rojo intenso y textura aterciopelada, con un sutil sabor a cacao, relleno y cubierto con frosting de queso crema.",
            Price = 130.00m,
            CategoryId = tortasPremiumId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688862/bakery/products/pwsdjyjh26eod13ifcoc.jpg" },
            LeadTimeInput = "3"
        });
        if (saladosId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Empanada de Carne Criolla",
            Description = "Sabor auténtico. Tierno relleno de carne de res cortada a cuchillo, cocinada lentamente con cebolla, pimentón y especias, en una masa casera.",
            Price = 7.50m,
            CategoryId = saladosId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688894/bakery/products/rbmwmoac9tc2udjearyk.jpg" }
        });
        if (masitasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Rollo de Canela Glaseado Individual",
            Description = "Aroma que enamora. Rollo de canela esponjoso y tierno, relleno de azúcar moreno y canela, cubierto con un delicioso glaseado de queso crema.",
            Price = 12.00m,
            CategoryId = masitasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688960/bakery/products/x60fsk4c9tgpjkktxyq7.jpg" }
        });
        if (bebidasId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Jugo Natural de Naranja Recién Exprimido",
            Description = "Vitamina C pura y refrescante. Jugo 100% natural de naranjas frescas, exprimido al momento para conservar todo su sabor y propiedades.",
            Price = 15.00m,
            CategoryId = bebidasId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747688986/bakery/products/cgz3rjcr9hyurgoyijs5.jpg" }
        });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Tarta de Manzana Vegana Rústica",
            Description = "Un clásico reinventado. Delicadas manzanas caramelizadas sobre una base de masa quebrada vegana, con un toque de canela. Simple y exquisita.",
            Price = 95.00m,
            CategoryId = veganoId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689060/bakery/products/e55fbzjvrhsz4j7s7wxh.jpg" },
            LeadTimeInput = "4"
        });
        if (veganoId != Guid.Empty) products.Add(new CreateProductDto
        {
            Name = "Barra Energética Vegana de Frutos Secos",
            Description = "El snack saludable perfecto. Combinación de dátiles, nueces, almendras, semillas de chía y un toque de cacao puro. Energía natural sin aditivos.",
            Price = 10.00m,
            CategoryId = veganoId,
            Images = new List<string> { "https://res.cloudinary.com/dkappxhfr/image/upload/v1747689144/bakery/products/felrfvy6jh9c9rl3rgih.jpg" }
        });

        return products;
    }
}
