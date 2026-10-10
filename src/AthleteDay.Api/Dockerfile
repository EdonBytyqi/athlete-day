# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/AthleteDay.Contracts/AthleteDay.Contracts.csproj src/AthleteDay.Contracts/
COPY src/AthleteDay.Core/AthleteDay.Core.csproj src/AthleteDay.Core/
COPY src/AthleteDay.Api/AthleteDay.Api.csproj src/AthleteDay.Api/
RUN dotnet restore src/AthleteDay.Api/AthleteDay.Api.csproj
COPY src/AthleteDay.Contracts/ src/AthleteDay.Contracts/
COPY src/AthleteDay.Core/ src/AthleteDay.Core/
COPY src/AthleteDay.Api/ src/AthleteDay.Api/
RUN dotnet publish src/AthleteDay.Api/AthleteDay.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AthleteDay.Api.dll"]
