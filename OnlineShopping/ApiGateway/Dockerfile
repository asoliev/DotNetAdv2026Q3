FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Shared/ShoppingAuth/ShoppingAuth.csproj Shared/ShoppingAuth/
COPY Shared/ShoppingTelemetry/ShoppingTelemetry.csproj Shared/ShoppingTelemetry/
COPY ApiGateway/src/ApiGateway.Api/ApiGateway.Api.csproj ApiGateway/src/ApiGateway.Api/

RUN dotnet restore ApiGateway/src/ApiGateway.Api/ApiGateway.Api.csproj

COPY Shared/ Shared/
COPY ApiGateway/src/ApiGateway.Api/ ApiGateway/src/ApiGateway.Api/

RUN dotnet publish ApiGateway/src/ApiGateway.Api/ApiGateway.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ApiGateway.Api.dll"]