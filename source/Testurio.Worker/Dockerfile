# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["source/Testurio.Worker/Testurio.Worker.csproj", "source/Testurio.Worker/"]
COPY ["source/Testurio.Core/Testurio.Core.csproj", "source/Testurio.Core/"]
COPY ["source/Testurio.Infrastructure/Testurio.Infrastructure.csproj", "source/Testurio.Infrastructure/"]
COPY ["source/Testurio.Pipeline.AgentRouter/Testurio.Pipeline.AgentRouter.csproj", "source/Testurio.Pipeline.AgentRouter/"]
COPY ["source/Testurio.Pipeline.Executors/Testurio.Pipeline.Executors.csproj", "source/Testurio.Pipeline.Executors/"]
COPY ["source/Testurio.Pipeline.Generators/Testurio.Pipeline.Generators.csproj", "source/Testurio.Pipeline.Generators/"]
COPY ["source/Testurio.Pipeline.MemoryRetrieval/Testurio.Pipeline.MemoryRetrieval.csproj", "source/Testurio.Pipeline.MemoryRetrieval/"]
COPY ["source/Testurio.Pipeline.StoryParser/Testurio.Pipeline.StoryParser.csproj", "source/Testurio.Pipeline.StoryParser/"]
COPY ["source/Testurio.Plugins/Testurio.Plugins.csproj", "source/Testurio.Plugins/"]

RUN dotnet restore "source/Testurio.Worker/Testurio.Worker.csproj"

COPY . .
WORKDIR /src/source/Testurio.Worker
RUN dotnet publish "Testurio.Worker.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime with Playwright Chromium
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install Playwright OS-level dependencies for Chromium
RUN apt-get update && apt-get install -y \
    libnss3 \
    libatk-bridge2.0-0 \
    libdrm2 \
    libxkbcommon0 \
    libgbm1 \
    libgtk-3-0 \
    libxshmfence1 \
    libasound2 \
    --no-install-recommends && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Install Playwright Chromium browser into the published app's Playwright browser path.
# The dotnet tool is not available in the runtime image; use the bundled playwright script instead.
ENV PLAYWRIGHT_BROWSERS_PATH=/app/.playwright
RUN dotnet /app/Microsoft.Playwright.dll install chromium

ENTRYPOINT ["dotnet", "Testurio.Worker.dll"]
