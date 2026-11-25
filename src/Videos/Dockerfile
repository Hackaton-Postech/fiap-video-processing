FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Videos/FiapVideo.Videos.Domain/*.csproj src/Videos/FiapVideo.Videos.Domain/
COPY src/Videos/FiapVideo.Videos.Application/*.csproj src/Videos/FiapVideo.Videos.Application/
COPY src/Videos/FiapVideo.Videos.Infrastructure/*.csproj src/Videos/FiapVideo.Videos.Infrastructure/
COPY src/Videos/FiapVideo.Videos.Api/*.csproj src/Videos/FiapVideo.Videos.Api/
RUN dotnet restore src/Videos/FiapVideo.Videos.Api/FiapVideo.Videos.Api.csproj
COPY src/Videos/ src/Videos/
RUN dotnet publish src/Videos/FiapVideo.Videos.Api/FiapVideo.Videos.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "FiapVideo.Videos.Api.dll"]
