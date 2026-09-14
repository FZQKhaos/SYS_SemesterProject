FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY src/src.csproj ./src/
RUN dotnet restore ./src/src.csproj

COPY src/. ./src/

RUN dotnet publish ./src/src.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "src.dll"]