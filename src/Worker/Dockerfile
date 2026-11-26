FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Worker/FiapVideo.Worker/*.csproj src/Worker/FiapVideo.Worker/
RUN dotnet restore src/Worker/FiapVideo.Worker/FiapVideo.Worker.csproj
COPY src/Worker/ src/Worker/
RUN dotnet publish src/Worker/FiapVideo.Worker/FiapVideo.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "FiapVideo.Worker.dll"]
