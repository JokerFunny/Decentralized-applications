# Stage 1: Runtime.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Stage 2: Build.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files (Worker also needs Shared).
COPY ["Worker/Worker.csproj", "Worker/"]
COPY ["Shared/Shared.csproj", "Shared/"]

# Restore dependencies
RUN dotnet restore "Worker/Worker.csproj"

# Copy everything and build.
COPY . .
WORKDIR "/src/Worker"
RUN dotnet build "Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]
