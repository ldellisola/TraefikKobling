FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env
WORKDIR /src

# Install AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

# Copy everything
COPY ./src ./

# Restore as distinct layers
RUN dotnet restore /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj -r linux-musl-arm64

# Build and publish a release
RUN dotnet publish /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj \
  -c Release \
  -o out \
  --no-restore \
  -r linux-musl-arm64

# Build runtime image - minimal Alpine
FROM alpine:3.21 AS runtime
RUN apk add --no-cache ca-certificates tzdata

ENV REDIS_URL=redis:6379
WORKDIR /app
COPY --from=build-env /src/out .
ENTRYPOINT ["./TraefikKobling.Worker"]