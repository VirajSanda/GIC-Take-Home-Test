# ==============================
# Stage 1: Build
# ==============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files explicitly
COPY Shared/Shared.csproj Shared/
COPY UserService/UserService.csproj UserService/

# Restore dependencies for this service
RUN dotnet restore UserService/UserService.csproj

# Copy all source code
COPY . .

# Publish the service
RUN dotnet publish UserService/UserService.csproj -c Release -o /app/publish

# ==============================
# Stage 2: Runtime
# ==============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]
