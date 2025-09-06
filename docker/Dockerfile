FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env
WORKDIR /src

# Install AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

# Copy everything
COPY ./src ./

# Set target architecture based on build platform
ARG TARGETARCH
RUN case ${TARGETARCH} in \
  "amd64") echo "linux-musl-x64" > /tmp/rid ;; \
  "arm64") echo "linux-musl-arm64" > /tmp/rid ;; \
  *) echo "Unsupported architecture: ${TARGETARCH}" && exit 1 ;; \
  esac

# Restore as distinct layers
RUN dotnet restore /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj -r $(cat /tmp/rid)

# Build and publish a release
RUN dotnet publish /src/TraefikKobling.Worker/TraefikKobling.Worker.csproj \
  -c Release \
  -o out \
  --no-restore \
  -r $(cat /tmp/rid)

# Build runtime image - minimal Alpine
FROM alpine:3.21 AS runtime
RUN apk add --no-cache ca-certificates tzdata && rm -rf /var/cache/apk/*

RUN adduser -D -s /bin/sh appuser
USER appuser

ENV REDIS_URL=redis:6379
ENV TRAEFIK_EXPORTER=redis
WORKDIR /app
COPY --from=build-env /src/out .
RUN mkdir ./config
ENTRYPOINT ["./TraefikKobling.Worker"]
