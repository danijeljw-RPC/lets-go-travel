FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ReadyToGoTravel.slnx --locked-mode
RUN dotnet publish src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ReadyToGoTravel.Worker.dll"]
