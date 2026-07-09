# ── Build ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia só os .csproj primeiro: o restore fica em cache enquanto as
# dependências não mudarem, mesmo que o código-fonte mude.
COPY ClickManagerApi/ClickManagerApi.csproj             ClickManagerApi/
COPY ClickManagerApi.Tests/ClickManagerApi.Tests.csproj ClickManagerApi.Tests/
RUN dotnet restore ClickManagerApi/ClickManagerApi.csproj

COPY . .
RUN dotnet publish ClickManagerApi/ClickManagerApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuário não-root: se a aplicação for comprometida, o atacante não
# tem privilégios de root dentro do container.
RUN adduser --disabled-password --gecos "" --uid 1001 appuser

COPY --from=build /app/publish .

# Diretório de uploads precisa existir e pertencer ao appuser
RUN mkdir -p /app/wwwroot/uploads && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "ClickManagerApi.dll"]
