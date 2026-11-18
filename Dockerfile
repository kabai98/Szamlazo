FROM node:24-bookworm-slim AS web
WORKDIR /src
COPY web/package*.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/Szamlazo.Api.csproj api/
COPY backend/Domain/Szamlazo.Domain.csproj backend/Domain/
COPY backend/Application/Szamlazo.Application.csproj backend/Application/
COPY backend/Infrastructure/Szamlazo.Infrastructure.csproj backend/Infrastructure/
RUN dotnet restore api/Szamlazo.Api.csproj
COPY backend/ backend/
COPY api/ api/
COPY --from=web /src/dist/szamlazo/browser/ api/wwwroot/
RUN dotnet publish api/Szamlazo.Api.csproj --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils tesseract-ocr tesseract-ocr-hun tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/* && install -d -o app -g app /data
WORKDIR /app
COPY --from=build /publish/ ./
USER app
ENTRYPOINT ["dotnet", "Szamlazo.Api.dll"]
