FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SensorSimulator/SensorSimulator.csproj src/SensorSimulator/
COPY src/Shared/Scada.Shared.Contracts/Scada.Shared.Contracts.csproj src/Shared/Scada.Shared.Contracts/
COPY src/Shared/Scada.Shared.Security/Scada.Shared.Security.csproj src/Shared/Scada.Shared.Security/
RUN dotnet restore src/SensorSimulator/SensorSimulator.csproj
COPY src/SensorSimulator/ src/SensorSimulator/
COPY src/Shared/Scada.Shared.Contracts/ src/Shared/Scada.Shared.Contracts/
COPY src/Shared/Scada.Shared.Security/ src/Shared/Scada.Shared.Security/
RUN dotnet publish src/SensorSimulator/SensorSimulator.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SensorSimulator.dll"]
