# Build context: repo root (needs the whole src/ tree for project references).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BKeeper.Domain/BKeeper.Domain.csproj src/BKeeper.Domain/
COPY src/BKeeper.Application/BKeeper.Application.csproj src/BKeeper.Application/
COPY src/BKeeper.Infrastructure/BKeeper.Infrastructure.csproj src/BKeeper.Infrastructure/
COPY src/BKeeper.Worker/BKeeper.Worker.csproj src/BKeeper.Worker/
RUN dotnet restore src/BKeeper.Worker/BKeeper.Worker.csproj
COPY src/BKeeper.Domain src/BKeeper.Domain
COPY src/BKeeper.Application src/BKeeper.Application
COPY src/BKeeper.Infrastructure src/BKeeper.Infrastructure
COPY src/BKeeper.Worker src/BKeeper.Worker
RUN dotnet publish src/BKeeper.Worker/BKeeper.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "BKeeper.Worker.dll"]
