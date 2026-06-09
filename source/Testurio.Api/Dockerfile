# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["source/Testurio.Api/Testurio.Api.csproj", "source/Testurio.Api/"]
COPY ["source/Testurio.Core/Testurio.Core.csproj", "source/Testurio.Core/"]
COPY ["source/Testurio.Infrastructure/Testurio.Infrastructure.csproj", "source/Testurio.Infrastructure/"]

RUN dotnet restore "source/Testurio.Api/Testurio.Api.csproj"

COPY . .

RUN dotnet publish "source/Testurio.Api/Testurio.Api.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Testurio.Api.dll"]
