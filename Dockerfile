# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src

COPY global.json ./
COPY sources/ ./sources/

RUN dotnet restore sources/Api/Api.csproj \
    && dotnet restore sources/WebApp/WebApp.csproj

FROM build AS api-publish
RUN dotnet publish sources/Api/Api.csproj \
    --configuration Release \
    --no-restore \
    --output /out/api \
    /p:UseAppHost=false

FROM build AS web-publish
RUN dotnet publish sources/WebApp/WebApp.csproj \
    --configuration Release \
    --no-restore \
    --output /out/web \
    /p:UseAppHost=false

FROM node:24.11.1-alpine AS react-build
WORKDIR /src

COPY sources/ReactApp/package.json sources/ReactApp/package-lock.json sources/ReactApp/.npmrc ./sources/ReactApp/
WORKDIR /src/sources/ReactApp
RUN npm ci

WORKDIR /src
COPY sources/ReactApp/ ./sources/ReactApp/
COPY sources/WebApp/Properties/Language.resx sources/WebApp/Properties/Language.de.resx ./sources/WebApp/Properties/
COPY sources/Common/Ui/routes.json ./sources/Common/Ui/routes.json
WORKDIR /src/sources/ReactApp
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=api-publish /out/api/ ./

RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p \
        /app/App_Data \
        /home/app/.aspnet/DataProtection-Keys \
        /var/lib/united-tactical-forces/hosting \
    && chown -R app:app \
        /app/App_Data \
        /home/app/.aspnet \
        /var/lib/united-tactical-forces

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "X39.UnitedTacticalForces.Api.dll"]

FROM nginx:1.29-alpine AS web

COPY deploy/nginx.conf /etc/nginx/nginx.conf
COPY --from=web-publish /out/web/wwwroot/ /usr/share/nginx/html/
COPY --from=react-build /src/sources/ReactApp/dist/ /usr/share/nginx/html/next/
COPY deploy/webapp.appsettings.json /usr/share/nginx/html/appsettings.json

# Runtime configuration is mounted over appsettings.json in production. Remove
# publish-generated environment overrides and compressed sidecars so nginx's
# gzip_static handling cannot serve stale build-time configuration instead.
RUN rm -f \
        /usr/share/nginx/html/appsettings.Production.json \
        /usr/share/nginx/html/appsettings*.json.br \
        /usr/share/nginx/html/appsettings*.json.gz

USER nginx
EXPOSE 8080
ENTRYPOINT ["nginx", "-g", "daemon off;"]
