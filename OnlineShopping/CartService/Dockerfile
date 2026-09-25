# --- Build stage ---
# Use the official .NET 10 SDK image to compile and publish the application.
# The SDK image is large but only used during build; it will not be included
# in the final image thanks to multi-stage builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so Docker can cache the NuGet restore layer.
# If only source code changes (not .csproj), this layer is reused and
# 'dotnet restore' is skipped — significantly speeding up rebuilds.
COPY Directory.Build.props ./
COPY Shared/ShoppingAuth/ShoppingAuth.csproj Shared/ShoppingAuth/
COPY CartService/src/CartService.Bll/CartService.Bll.csproj CartService/src/CartService.Bll/
COPY CartService/src/CartService.Dal/CartService.Dal.csproj CartService/src/CartService.Dal/
COPY CartService/src/CartService.Api/CartService.Api.csproj CartService/src/CartService.Api/

# Restore NuGet packages for the entry-point project.
RUN dotnet restore CartService/src/CartService.Api/CartService.Api.csproj

# Copy the rest of the source code.
COPY Shared/ Shared/
COPY CartService/src/ CartService/src/

# Publish in Release mode to /app/publish.
# --no-restore reuses the packages restored above.
RUN dotnet publish CartService/src/CartService.Api/CartService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Runtime stage ---
# Use the slim ASP.NET Core runtime image (~220MB).
# The SDK is NOT present here — smaller image and smaller attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create a directory for the LiteDB database file.
# Mounting a Docker volume here ensures the database survives container restarts.
RUN mkdir -p /app/data

# Copy only the published output from the build stage.
COPY --from=build /app/publish .

# Tell the app to store the LiteDB database in the data directory.
ENV DATABASE_DIRECTORY=/app/data

# ASP.NET Core listens on port 8080 inside the container by default (since .NET 8).
EXPOSE 8080

ENTRYPOINT ["dotnet", "CartService.Api.dll"]
