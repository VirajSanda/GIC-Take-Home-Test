# ==============================
# Stage 1: Build
# ==============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files separately to leverage layer caching
COPY Shared/Shared.csproj Shared/
COPY OrderService/OrderService.csproj OrderService/

# Restore dependencies
RUN dotnet restore "OrderService/OrderService.csproj"

# Copy all source code
COPY . .

# Build and publish (disable app host for smaller image)
WORKDIR /src/OrderService
RUN dotnet publish "OrderService.csproj" -c Release -o /out /p:UseAppHost=false

# ==============================
# Stage 2: Runtime
# ==============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Ensure target folder exists and has proper permissions
RUN mkdir -p /app && chmod -R 755 /app

# Copy published files from build stage
COPY --from=build /out .

# Expose default port (optional, but helpful)
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderService.dll"]
