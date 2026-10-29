# syntax=docker/dockerfile:1
# Publishes the Native AOT binary (ADR 0002) on the SDK image, then runs it on a
# minimal runtime-deps base. The push to ghcr.io/egoushka/subgate belongs to #6.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble-aot
ARG DOTNET_RUNTIME_DEPS_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled

FROM ${DOTNET_SDK_IMAGE} AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props subgate.slnx ./
COPY src/Subgate/Subgate.csproj src/Subgate/
RUN case "$TARGETARCH" in \
      amd64) echo linux-x64 ;; \
      arm64) echo linux-arm64 ;; \
      *) echo "unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac > /tmp/rid
RUN dotnet restore src/Subgate/Subgate.csproj -r "$(cat /tmp/rid)"

COPY src/Subgate/ src/Subgate/
RUN dotnet publish src/Subgate/Subgate.csproj \
      -c Release \
      -r "$(cat /tmp/rid)" \
      --no-restore \
      -o /app/publish

FROM ${DOTNET_RUNTIME_DEPS_IMAGE} AS runtime
WORKDIR /app
COPY --from=build /app/publish/Subgate ./subgate
ENTRYPOINT ["/app/subgate"]
