# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["VoiceAgentApi.csproj", "./"]
RUN dotnet restore "VoiceAgentApi.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "VoiceAgentApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "VoiceAgentApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published files
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "VoiceAgentApi.dll"]
