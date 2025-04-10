# --- Etapa 1: Entorno de Compilación ---
    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    ARG API_PROJECT=BakeryHub.Api # Mantener esto para usarlo después
    
    # Copiar .sln y .csproj (sin cambios)
    COPY ["BakeryHub.sln", "./"]
    COPY ["BakeryHub.Api/BakeryHub.Api.csproj", "BakeryHub.Api/"]
    COPY ["BakeryHub.Application/BakeryHub.Application.csproj", "BakeryHub.Application/"]
    COPY ["BakeryHub.Domain/BakeryHub.Domain.csproj", "BakeryHub.Domain/"]
    COPY ["BakeryHub.Infrastructure/BakeryHub.Infrastructure.csproj", "BakeryHub.Infrastructure/"]
    
    # --- Restaurar la SOLUCIÓN COMPLETA ---
    RUN dotnet restore "./BakeryHub.sln"
    # --------------------------------------
    
    # Copiar el resto del código fuente (después de restaurar para mejor caché)
    COPY . .
    
    # Publicar el proyecto API (sin cambios aquí, sigue usando el ARG)
    WORKDIR "/src/${API_PROJECT}"
    RUN dotnet publish "./${API_PROJECT}.csproj" -c Release -o /app/publish --no-restore
    
    # --- Etapa 2: Imagen Final (sin cambios) ---
    FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
    # ...(resto sin cambios)...
    ENTRYPOINT ["dotnet", "BakeryHub.Api.dll"]