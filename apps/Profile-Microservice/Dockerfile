FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/Profile-Microservice/src/src.csproj", "apps/Profile-Microservice/src/"]
COPY ["Shared/Bizcord.Shared/Bizcord.Shared.csproj", "Shared/Bizcord.Shared/"]
RUN dotnet restore "apps/Profile-Microservice/src/src.csproj"
COPY . .
WORKDIR /src/apps/Profile-Microservice/src
RUN dotnet build "src.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "src.dll"]
