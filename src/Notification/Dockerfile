FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Notification/FiapVideo.Notification/*.csproj src/Notification/FiapVideo.Notification/
RUN dotnet restore src/Notification/FiapVideo.Notification/FiapVideo.Notification.csproj
COPY src/Notification/ src/Notification/
RUN dotnet publish src/Notification/FiapVideo.Notification/FiapVideo.Notification.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "FiapVideo.Notification.dll"]
