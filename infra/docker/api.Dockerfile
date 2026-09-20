# Build context: repo root (needs the whole src/ tree for project references).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BKeeper.Domain/BKeeper.Domain.csproj src/BKeeper.Domain/
COPY src/BKeeper.Application/BKeeper.Application.csproj src/BKeeper.Application/
COPY src/BKeeper.Infrastructure/BKeeper.Infrastructure.csproj src/BKeeper.Infrastructure/
COPY src/BKeeper.Api/BKeeper.Api.csproj src/BKeeper.Api/
RUN dotnet restore src/BKeeper.Api/BKeeper.Api.csproj
COPY src/BKeeper.Domain src/BKeeper.Domain
COPY src/BKeeper.Application src/BKeeper.Application
COPY src/BKeeper.Infrastructure src/BKeeper.Infrastructure
COPY src/BKeeper.Api src/BKeeper.Api
RUN dotnet publish src/BKeeper.Api/BKeeper.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=10 CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "BKeeper.Api.dll"]
