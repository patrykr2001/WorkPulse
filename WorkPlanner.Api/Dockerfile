# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY WorkPlanner.slnx .
COPY WorkPlanner.Api/WorkPlanner.Api.csproj WorkPlanner.Api/
COPY WorkPlanner.Client/WorkPlanner.Client.csproj WorkPlanner.Client/

# Restore dependencies
RUN dotnet restore WorkPlanner.Api/WorkPlanner.Api.csproj

# Copy source code
COPY WorkPlanner.Api/ WorkPlanner.Api/

# Build and publish
WORKDIR /src/WorkPlanner.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create directory for SQLite database
RUN mkdir -p /app/data

# Copy published files
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Volume for SQLite database persistence
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "WorkPlanner.Api.dll"]
