# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ProjectAPI.sln .
COPY ["ProjectAPI/ProjectAPI.csproj", "ProjectAPI/"]

# Restore dependencies
RUN dotnet restore "ProjectAPI/ProjectAPI.csproj"

# Copy all files and set working directory
COPY . .
WORKDIR /src/ProjectAPI

# Build and publish
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Expose port 80
EXPOSE 80

# Run the application
ENTRYPOINT ["dotnet", "ProjectAPI.dll"]
