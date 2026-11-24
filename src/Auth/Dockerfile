FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Auth/FiapVideo.Auth.Domain/*.csproj src/Auth/FiapVideo.Auth.Domain/
COPY src/Auth/FiapVideo.Auth.Application/*.csproj src/Auth/FiapVideo.Auth.Application/
COPY src/Auth/FiapVideo.Auth.Infrastructure/*.csproj src/Auth/FiapVideo.Auth.Infrastructure/
COPY src/Auth/FiapVideo.Auth.Api/*.csproj src/Auth/FiapVideo.Auth.Api/
RUN dotnet restore src/Auth/FiapVideo.Auth.Api/FiapVideo.Auth.Api.csproj
COPY src/Auth/ src/Auth/
RUN dotnet publish src/Auth/FiapVideo.Auth.Api/FiapVideo.Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "FiapVideo.Auth.Api.dll"]
