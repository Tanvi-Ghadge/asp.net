# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies (layer-cached separately from source)
COPY ["src/EnterpriseApp.API/EnterpriseApp.API.csproj", "EnterpriseApp.API/"]
COPY ["src/EnterpriseApp.Application/EnterpriseApp.Application.csproj", "EnterpriseApp.Application/"]
COPY ["src/EnterpriseApp.Domain/EnterpriseApp.Domain.csproj", "EnterpriseApp.Domain/"]
COPY ["src/EnterpriseApp.Infrastructure/EnterpriseApp.Infrastructure.csproj", "EnterpriseApp.Infrastructure/"]

RUN dotnet restore "EnterpriseApp.API/EnterpriseApp.API.csproj"

# Copy source and build
COPY src/ .
RUN dotnet build "EnterpriseApp.API/EnterpriseApp.API.csproj" -c Release -o /app/build

# ── Publish stage ──────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "EnterpriseApp.API/EnterpriseApp.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# # Non-root user for security
# RUN adduser --disabled-password --gecos "" appuser
# USER appuser

# # Logs volume — ensure writable
# RUN mkdir -p /app/logs
# VOLUME ["/app/logs"]
RUN mkdir -p /app/logs

RUN adduser --disabled-password --gecos "" appuser
RUN chown -R appuser:appuser /app

USER appuser

VOLUME ["/app/logs"]

COPY --from=publish /app/publish .

# Expose HTTP only; TLS is terminated at the load balancer
EXPOSE 8080

ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EnterpriseApp.API.dll"]
