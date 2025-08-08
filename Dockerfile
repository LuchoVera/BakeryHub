FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BakeryHub.sln", "."]
COPY ["BakeryHub.Host/BakeryHub.Host.csproj", "BakeryHub.Host/"]
COPY ["BakeryHub.Application/BakeryHub.Application.csproj", "BakeryHub.Application/"]
COPY ["BakeryHub.Domain/BakeryHub.Domain.csproj", "BakeryHub.Domain/"]
COPY ["BakeryHub.Infrastructure/BakeryHub.Infrastructure.csproj", "BakeryHub.Infrastructure/"]
COPY ["BakeryHub.Shared.Kernel/BakeryHub.Shared.Kernel.csproj", "BakeryHub.Shared.Kernel/"]

COPY ["BakeryHub.Modules.Accounts.Api/BakeryHub.Modules.Accounts.Api.csproj", "BakeryHub.Modules.Accounts.Api/"]
COPY ["BakeryHub.Modules.Accounts.Application/BakeryHub.Modules.Accounts.Application.csproj", "BakeryHub.Modules.Accounts.Application/"]
COPY ["BakeryHub.Modules.Accounts.Domain/BakeryHub.Modules.Accounts.Domain.csproj", "BakeryHub.Modules.Accounts.Domain/"]
COPY ["BakeryHub.Modules.Accounts.Infrastructure/BakeryHub.Modules.Accounts.Infrastructure.csproj", "BakeryHub.Modules.Accounts.Infrastructure/"]

COPY ["BakeryHub.Modules.Catalog.Api/BakeryHub.Modules.Catalog.Api.csproj", "BakeryHub.Modules.Catalog.Api/"]
COPY ["BakeryHub.Modules.Catalog.Application/BakeryHub.Modules.Catalog.Application.csproj", "BakeryHub.Modules.Catalog.Application/"]
COPY ["BakeryHub.Modules.Catalog.Domain/BakeryHub.Modules.Catalog.Domain.csproj", "BakeryHub.Modules.Catalog.Domain/"]
COPY ["BakeryHub.Modules.Catalog.Infrastructure/BakeryHub.Modules.Catalog.Infrastructure.csproj", "BakeryHub.Modules.Catalog.Infrastructure/"]

COPY ["BakeryHub.Modules.Dashboard.Api/BakeryHub.Modules.Dashboard.Api.csproj", "BakeryHub.Modules.Dashboard.Api/"]
COPY ["BakeryHub.Modules.Dashboard.Application/BakeryHub.Modules.Dashboard.Application.csproj", "BakeryHub.Modules.Dashboard.Application/"]
COPY ["BakeryHub.Modules.Dashboard.Domain/BakeryHub.Modules.Dashboard.Domain.csproj", "BakeryHub.Modules.Dashboard.Domain/"]
COPY ["BakeryHub.Modules.Dashboard.Infrastructure/BakeryHub.Modules.Dashboard.Infrastructure.csproj", "BakeryHub.Modules.Dashboard.Infrastructure/"]

COPY ["BakeryHub.Modules.Orders.Api/BakeryHub.Modules.Orders.Api.csproj", "BakeryHub.Modules.Orders.Api/"]
COPY ["BakeryHub.Modules.Orders.Application/BakeryHub.Modules.Orders.Application.csproj", "BakeryHub.Modules.Orders.Application/"]
COPY ["BakeryHub.Modules.Orders.Domain/BakeryHub.Modules.Orders.Domain.csproj", "BakeryHub.Modules.Orders.Domain/"]
COPY ["BakeryHub.Modules.Orders.Infrastructure/BakeryHub.Modules.Orders.Infrastructure.csproj", "BakeryHub.Modules.Orders.Infrastructure/"]

COPY ["BakeryHub.Modules.Recommendations.Api/BakeryHub.Modules.Recommendations.Api.csproj", "BakeryHub.Modules.Recommendations.Api/"]
COPY ["BakeryHub.Modules.Recommendations.Application/BakeryHub.Modules.Recommendations.Application.csproj", "BakeryHub.Modules.Recommendations.Application/"]
COPY ["BakeryHub.Modules.Recommendations.Domain/BakeryHub.Modules.Recommendations.Domain.csproj", "BakeryHub.Modules.Recommendations.Domain/"]
COPY ["BakeryHub.Modules.Recommendations.Infrastructure/BakeryHub.Modules.Recommendations.Infrastructure.csproj", "BakeryHub.Modules.Recommendations.Infrastructure/"]

COPY ["BakeryHub.Modules.Tenants.Api/BakeryHub.Modules.Tenants.Api.csproj", "BakeryHub.Modules.Tenants.Api/"]
COPY ["BakeryHub.Modules.Tenants.Application/BakeryHub.Modules.Tenants.Application.csproj", "BakeryHub.Modules.Tenants.Application/"]
COPY ["BakeryHub.Modules.Tenants.Domain/BakeryHub.Modules.Tenants.Domain.csproj", "BakeryHub.Modules.Tenants.Domain/"]
COPY ["BakeryHub.Modules.Tenants.Infrastructure/BakeryHub.Modules.Tenants.Infrastructure.csproj", "BakeryHub.Modules.Tenants.Infrastructure/"]

RUN dotnet restore "BakeryHub.sln"

COPY . .
WORKDIR "/src/BakeryHub.Host"
RUN dotnet build "BakeryHub.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BakeryHub.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BakeryHub.Host.dll"]
