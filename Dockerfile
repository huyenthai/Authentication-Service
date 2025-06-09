# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY "Authentication Service.sln" .
COPY "src/Authentication Service/Authentication Service.csproj" "src/Authentication Service/"

# Restore
RUN dotnet restore "./Authentication Service.sln"

# Copy entire source
COPY ./src ./src

# Publish
RUN dotnet publish "src/Authentication Service/Authentication Service.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Authentication Service.dll"]
