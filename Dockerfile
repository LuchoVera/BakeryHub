FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BakeryHub.Api/BakeryHub.Api.csproj", "BakeryHub.Api/"]
COPY ["BakeryHub.Application/BakeryHub.Application.csproj", "BakeryHub.Application/"]
COPY ["BakeryHub.Domain/BakeryHub.Domain.csproj", "BakeryHub.Domain/"]
COPY ["BakeryHub.Infrastructure/BakeryHub.Infrastructure.csproj", "BakeryHub.Infrastructure/"]

RUN dotnet restore "BakeryHub.Api/BakeryHub.Api.csproj"
COPY . .
WORKDIR "/src/BakeryHub.Api"
RUN dotnet build "BakeryHub.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BakeryHub.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BakeryHub.Api.dll"]
