FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first, from the project files only, so the layer is cached across code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Demo.Api/Demo.Api.csproj src/Demo.Api/
RUN dotnet restore src/Demo.Api/Demo.Api.csproj
COPY src/ src/
RUN dotnet publish src/Demo.Api/Demo.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.Api.dll"]
