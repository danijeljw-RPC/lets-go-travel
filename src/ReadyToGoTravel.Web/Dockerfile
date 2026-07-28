FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ReadyToGoTravel.slnx --locked-mode
RUN dotnet publish src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ReadyToGoTravel.Web.dll"]
