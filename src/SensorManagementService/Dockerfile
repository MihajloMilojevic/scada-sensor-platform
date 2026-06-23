FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SensorManagementService/SensorManagementService.csproj src/SensorManagementService/
COPY src/Shared/Scada.Shared.Contracts/Scada.Shared.Contracts.csproj src/Shared/Scada.Shared.Contracts/
COPY src/Shared/Scada.Shared.Kafka/Scada.Shared.Kafka.csproj src/Shared/Scada.Shared.Kafka/
RUN dotnet restore src/SensorManagementService/SensorManagementService.csproj
COPY src/SensorManagementService/ src/SensorManagementService/
COPY src/Shared/Scada.Shared.Contracts/ src/Shared/Scada.Shared.Contracts/
COPY src/Shared/Scada.Shared.Kafka/ src/Shared/Scada.Shared.Kafka/
RUN dotnet publish src/SensorManagementService/SensorManagementService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SensorManagementService.dll"]
