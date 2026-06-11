# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["source/Testurio.Worker/Testurio.Worker.csproj", "source/Testurio.Worker/"]
COPY ["source/Testurio.Core/Testurio.Core.csproj", "source/Testurio.Core/"]
COPY ["source/Testurio.Infrastructure/Testurio.Infrastructure.csproj", "source/Testurio.Infrastructure/"]
COPY ["source/Testurio.Pipeline.AgentRouter/Testurio.Pipeline.AgentRouter.csproj", "source/Testurio.Pipeline.AgentRouter/"]
COPY ["source/Testurio.Pipeline.Executors/Testurio.Pipeline.Executors.csproj", "source/Testurio.Pipeline.Executors/"]
COPY ["source/Testurio.Pipeline.Generators/Testurio.Pipeline.Generators.csproj", "source/Testurio.Pipeline.Generators/"]
COPY ["source/Testurio.Pipeline.MemoryRetrieval/Testurio.Pipeline.MemoryRetrieval.csproj", "source/Testurio.Pipeline.MemoryRetrieval/"]
COPY ["source/Testurio.Pipeline.StoryParser/Testurio.Pipeline.StoryParser.csproj", "source/Testurio.Pipeline.StoryParser/"]
COPY ["source/Testurio.Pipeline.FeedbackLoop/Testurio.Pipeline.FeedbackLoop.csproj", "source/Testurio.Pipeline.FeedbackLoop/"]
COPY ["source/Testurio.Pipeline.MemoryWriter/Testurio.Pipeline.MemoryWriter.csproj", "source/Testurio.Pipeline.MemoryWriter/"]
COPY ["source/Testurio.Pipeline.ReportWriter/Testurio.Pipeline.ReportWriter.csproj", "source/Testurio.Pipeline.ReportWriter/"]
COPY ["source/Testurio.Plugins/Testurio.Plugins.csproj", "source/Testurio.Plugins/"]

RUN dotnet restore "source/Testurio.Worker/Testurio.Worker.csproj"

COPY . .
WORKDIR /src/source/Testurio.Worker
RUN dotnet publish "Testurio.Worker.csproj" -c Release -o /app/publish --no-restore

# Install Playwright browsers in the build stage (SDK available here)
ENV PLAYWRIGHT_BROWSERS_PATH=/app/publish/.playwright
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"
RUN playwright install chromium

# Stage 2: Runtime with Playwright Chromium
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
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
    libasound2t64 \
    --no-install-recommends && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV PLAYWRIGHT_BROWSERS_PATH=/app/.playwright

ENTRYPOINT ["dotnet", "Testurio.Worker.dll"]
