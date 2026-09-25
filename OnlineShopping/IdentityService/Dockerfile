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
COPY IdentityService/src/IdentityService.Api/IdentityService.Api.csproj IdentityService/src/IdentityService.Api/

# Restore NuGet packages for the entry-point project.
RUN dotnet restore IdentityService/src/IdentityService.Api/IdentityService.Api.csproj

# Copy the rest of the source code.
COPY Shared/ Shared/
COPY IdentityService/src/ IdentityService/src/

# Publish in Release mode to /app/publish.
# --no-restore reuses the packages restored above.
RUN dotnet publish IdentityService/src/IdentityService.Api/IdentityService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Runtime stage ---
# Use the slim ASP.NET Core runtime image (~220MB).
# The SDK is NOT present here — smaller image and smaller attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy only the published output from the build stage.
COPY --from=build /app/publish .

# ASP.NET Core listens on port 8080 inside the container by default (since .NET 8).
EXPOSE 8080

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
