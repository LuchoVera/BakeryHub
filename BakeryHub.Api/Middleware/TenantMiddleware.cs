using Microsoft.Extensions.Primitives; // Para StringValues


namespace BakeryHub.Api.Middleware;
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        public const string TenantIdHeaderName = "X-Tenant-ID";
        // Clave para guardar/leer en HttpContext.Items (debe ser consistente)
        private const string TenantContextItemsKey = "TenantId_MyApp";

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // --- MÉTODO InvokeAsync SIMPLIFICADO ---
        // Ya NO inyecta IServiceProvider ni DbContext
        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.Headers.TryGetValue(TenantIdHeaderName, out StringValues tenantIdFromHeader);
            string? tenantIdLower = tenantIdFromHeader.FirstOrDefault()?.ToLowerInvariant();

            // 1. SOLO LEER y ESTABLECER en HttpContext.Items si la cabecera existe y tiene valor
            if (!string.IsNullOrEmpty(tenantIdLower))
            {
                context.Items[TenantContextItemsKey] = tenantIdLower;
                // Log opcional para confirmar que se estableció
                Console.WriteLine($"DEBUG [Middleware]: Set context.Items['{TenantContextItemsKey}'] = '{tenantIdLower}'");
            }
            else
            {
                // Log opcional si no se encontró
                Console.WriteLine($"DEBUG [Middleware]: Header '{TenantIdHeaderName}' not found or empty.");
                // No establecemos nada en Items. El DbContext obtendrá null más tarde.
            }

            // 2. Ya NO hay validación contra la base de datos aquí.
            // 3. Ya NO hay comprobación de 'requiresTenantHeader'.
            //    Si una ruta requiere un tenant y no se proporcionó uno válido en la cabecera,
            //    el DbContext obtendrá null, y el filtro global no devolverá datos para Product,
            //    o los servicios/controladores devolverán NotFound/BadRequest al no encontrar el tenant.

            // 4. Continuar SIEMPRE con el siguiente middleware en el pipeline
            await _next(context);
        }
        // --- FIN MÉTODO InvokeAsync SIMPLIFICADO ---
    }
