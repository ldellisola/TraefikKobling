FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build-env
WORKDIR /src

# Copy everything
COPY ./src ./

# Restore as distinct layers
RUN dotnet restore /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj

# Build and publish a release
RUN dotnet publish /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj \
    -c Release \
    -o out \
    --no-restore

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine

WORKDIR /app
COPY --from=build-env /src/out .
ENTRYPOINT ["dotnet","TraefikKobling.Worker.dll"]
