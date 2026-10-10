# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/AthleteDay.Contracts/AthleteDay.Contracts.csproj src/AthleteDay.Contracts/
COPY src/AthleteDay.Staff/AthleteDay.Staff.csproj src/AthleteDay.Staff/
RUN dotnet restore src/AthleteDay.Staff/AthleteDay.Staff.csproj
COPY src/AthleteDay.Contracts/ src/AthleteDay.Contracts/
COPY src/AthleteDay.Staff/ src/AthleteDay.Staff/
RUN dotnet publish src/AthleteDay.Staff/AthleteDay.Staff.csproj -c Release -o /app --no-restore

FROM nginx:1.27-alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
# Same-origin API calls through the nginx proxy below.
RUN echo '{ "ApiBaseUrl": "" }' > /usr/share/nginx/html/appsettings.json
COPY src/AthleteDay.Staff/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
