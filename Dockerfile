# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["ApiTestingMcp.Server/ApiTestingMcp.Server.csproj", "ApiTestingMcp.Server/"]
RUN dotnet restore "ApiTestingMcp.Server/ApiTestingMcp.Server.csproj"

# Copy source code and build release
COPY . .
WORKDIR "/src/ApiTestingMcp.Server"
RUN dotnet publish "ApiTestingMcp.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port configuration (Standard ASP.NET Core container port)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiTestingMcp.Server.dll"]
