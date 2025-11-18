FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Ports are configured in Program.cs via appsettings or environment variables
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Ae.Rail.dll"]


