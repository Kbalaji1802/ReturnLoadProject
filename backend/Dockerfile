# syntax=docker/dockerfile:1
#
# Multi-stage build for the ReturnLoad API. Targets .NET 9 (see 06_DECISION_LOG.md
# ADR-0006). Build the image from the `backend/` directory as the context.

# ---- Build stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy manifests first so `restore` is cached until a dependency actually changes.
COPY ReturnLoad.sln Directory.Build.props ./
COPY src/ReturnLoad.Shared/ReturnLoad.Shared.csproj src/ReturnLoad.Shared/
COPY src/ReturnLoad.Domain/ReturnLoad.Domain.csproj src/ReturnLoad.Domain/
COPY src/ReturnLoad.Application/ReturnLoad.Application.csproj src/ReturnLoad.Application/
COPY src/ReturnLoad.Infrastructure/ReturnLoad.Infrastructure.csproj src/ReturnLoad.Infrastructure/
COPY src/ReturnLoad.Api/ReturnLoad.Api.csproj src/ReturnLoad.Api/

RUN dotnet restore src/ReturnLoad.Api/ReturnLoad.Api.csproj

# Copy the remaining sources and publish a framework-dependent build.
COPY src/ src/
RUN dotnet publish src/ReturnLoad.Api/ReturnLoad.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Run as the image's built-in non-root user (least privilege — 01_PROJECT_RULES.md §5).
USER $APP_UID

# Kestrel listens on 8080 (non-privileged port, so the non-root user can bind it).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReturnLoad.Api.dll"]
