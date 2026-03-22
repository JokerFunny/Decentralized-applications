# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# 1. Base runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5001
EXPOSE 8081

# 2. Build image.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the project files and restore dependencies.
COPY ["DecentralizedChat.PeerNode/DecentralizedChat.PeerNode.csproj", "DecentralizedChat.PeerNode/"]
COPY ["DecentralizedChat.Shared/DecentralizedChat.Shared.csproj", "DecentralizedChat.Shared/"]
RUN dotnet restore "DecentralizedChat.PeerNode/DecentralizedChat.PeerNode.csproj"

# Copy the rest of the source code.
COPY . .
WORKDIR "/src/DecentralizedChat.PeerNode"
RUN dotnet build "DecentralizedChat.PeerNode.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 3. Publish.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DecentralizedChat.PeerNode.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 4. Final runtime image.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DecentralizedChat.PeerNode.dll"]
