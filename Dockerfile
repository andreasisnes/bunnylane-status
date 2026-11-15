# syntax=docker/dockerfile:1
FROM node:22-alpine AS frontend
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/BunnyLane.Status/BunnyLane.Status.csproj BunnyLane.Status/
RUN dotnet restore BunnyLane.Status/BunnyLane.Status.csproj
COPY backend/BunnyLane.Status/ BunnyLane.Status/
COPY --from=frontend /src/dist BunnyLane.Status/wwwroot
RUN dotnet publish BunnyLane.Status/BunnyLane.Status.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache tzdata icu-libs && addgroup -S status && adduser -S -G status -u 10001 status \
    && mkdir -p /data && chown status:status /data
COPY --from=backend /app ./
USER status
ENV ASPNETCORE_URLS=http://+:8080 \
    STATUS__SqlitePath=/data/status.db
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "BunnyLane.Status.dll"]
