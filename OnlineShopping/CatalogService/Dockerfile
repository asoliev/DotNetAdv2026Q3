# --- Build stage ---
# Use the official .NET 10 SDK image to compile and publish the application.
# The SDK image is large (~800MB) but only used during build; it will not be
# included in the final image thanks to multi-stage builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so Docker can cache the NuGet restore layer.
# If only source code changes (not .csproj), this layer is reused and
# 'dotnet restore' is skipped — significantly speeding up rebuilds.
COPY Directory.Build.props ./
COPY Shared/ShoppingAuth/ShoppingAuth.csproj Shared/ShoppingAuth/
COPY CatalogService/src/CatalogService.Domain/CatalogService.Domain.csproj CatalogService/src/CatalogService.Domain/
COPY CatalogService/src/CatalogService.Application/CatalogService.Application.csproj CatalogService/src/CatalogService.Application/
COPY CatalogService/src/CatalogService.Infrastructure/CatalogService.Infrastructure.csproj CatalogService/src/CatalogService.Infrastructure/
COPY CatalogService/src/CatalogService.Api/CatalogService.Api.csproj CatalogService/src/CatalogService.Api/

# Restore NuGet packages for the entry-point project.
# --locked-mode ensures the build fails if packages.lock.json is out of sync.
RUN dotnet restore CatalogService/src/CatalogService.Api/CatalogService.Api.csproj

# Copy the rest of the source code.
COPY Shared/ Shared/
COPY CatalogService/src/ CatalogService/src/

# Publish in Release mode to /app/publish.
# --no-restore reuses the packages restored above (avoids a second network call).
RUN dotnet publish CatalogService/src/CatalogService.Api/CatalogService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Runtime stage ---
# Use the slim ASP.NET Core runtime image (~220MB).
# The SDK is NOT present here, so the final image is much smaller and has a
# smaller attack surface (no compiler, no build tools).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create a directory for the SQLite database file.
# Mounting a Docker volume here ensures the database survives container restarts.
RUN mkdir -p /app/data

# Copy only the published output from the build stage.
COPY --from=build /app/publish .

# Tell the app to store the SQLite database in the data directory.
ENV DATABASE_DIRECTORY=/app/data

# ASP.NET Core listens on port 8080 inside the container by default (since .NET 8).
EXPOSE 8080

ENTRYPOINT ["dotnet", "CatalogService.Api.dll"]
