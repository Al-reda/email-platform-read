# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Shared.csproj            src/Shared/
COPY src/Read.Api/Read.Api.csproj        src/Read.Api/
RUN dotnet restore src/Read.Api/Read.Api.csproj

COPY src/Shared/      src/Shared/
COPY src/Read.Api/    src/Read.Api/

RUN dotnet publish src/Read.Api/Read.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EmailPlatform.Read.Api.dll"]
