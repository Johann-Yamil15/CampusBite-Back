# JJ-Sprint2 09/10/2026: imagen de la API con arquitectura limpia (src/CampusBite.Api + sus capas)

# Imagen base para ejecutar (también la usa Visual Studio en modo rápido)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Compilación: primero se copian solo los .csproj para aprovechar la caché de la restauración
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/CampusBite.Domain/CampusBite.Domain.csproj", "src/CampusBite.Domain/"]
COPY ["src/CampusBite.Application/CampusBite.Application.csproj", "src/CampusBite.Application/"]
COPY ["src/CampusBite.Infrastructure/CampusBite.Infrastructure.csproj", "src/CampusBite.Infrastructure/"]
COPY ["src/CampusBite.Api/CampusBite.Api.csproj", "src/CampusBite.Api/"]
RUN dotnet restore "src/CampusBite.Api/CampusBite.Api.csproj"
COPY src/ src/
RUN dotnet build "src/CampusBite.Api/CampusBite.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publicación
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/CampusBite.Api/CampusBite.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Imagen final (AssemblyName de la Api sigue siendo CampusBite-Back)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CampusBite-Back.dll"]
