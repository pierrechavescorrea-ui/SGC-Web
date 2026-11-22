# SigWeb en contenedor (AWS ECS Fargate o Elastic Beanstalk con Docker). Guía: docs/aws-despliegue.md
# Construir:  docker build -t sigweb .
# Probar:     docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="Host=...;Database=sigweb;Username=...;Password=..." sigweb

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SigWeb.csproj ./
RUN dotnet restore SigWeb.csproj
COPY . .
RUN dotnet publish SigWeb.csproj -c Release -o /app --no-restore /p:UseAppHost=false \
    && test -s /app/asistente-interfaz.json && test -n "$(ls /app/docs/manual/*.md)"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
# Archivos subidos por los usuarios: en AWS se monta un volumen EFS en esta ruta (no queda dentro de la imagen).
RUN mkdir -p /app/wwwroot/uploads /app/App_Data && chown -R $APP_UID /app/wwwroot/uploads /app/App_Data
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=America/Bogota
EXPOSE 8080
# Usuario sin privilegios (lo trae la imagen oficial de .NET).
USER $APP_UID
ENTRYPOINT ["dotnet", "SigWeb.dll"]
