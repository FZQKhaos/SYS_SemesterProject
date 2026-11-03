FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/Auth-Service/src/AuthService.csproj", "apps/Auth-Service/src/"]
RUN dotnet restore "apps/Auth-Service/src/AuthService.csproj"
COPY . .
WORKDIR /src/apps/Auth-Service/src
RUN dotnet publish "AuthService.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]