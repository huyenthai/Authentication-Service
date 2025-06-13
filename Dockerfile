# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY "AuthenticationService.sln" .
COPY "src/AuthenticationService/AuthenticationService.csproj" "src/AuthenticationService/"
COPY "tests/AuthenticationService.IntegrationTests/AuthenticationService.IntegrationTests.csproj" "tests/AuthenticationService.IntegrationTests/"
COPY "tests/AuthenticationService.UnitTests/AuthenticationService.UnitTests.csproj tests/AuthenticationService.UnitTests/"

# Restore
RUN dotnet restore "./AuthenticationService.sln"

# Copy entire source
COPY ./src ./src

# Publish
RUN dotnet publish "src/AuthenticationService/AuthenticationService.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthenticationService.dll"]
