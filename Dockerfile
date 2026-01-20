# Base stage for restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
ARG PROJECT=SiberVatan

COPY ["SiberVatan.csproj", "./"]
RUN --mount=type=cache,id=nuget-cache,target=/root/.nuget/packages \
    dotnet restore "SiberVatan.csproj"

# Build stage
FROM restore AS build
WORKDIR /src
ARG PROJECT=SiberVatan

COPY . .
RUN --mount=type=cache,id=nuget-cache,target=/root/.nuget/packages \
    dotnet build "SiberVatan.csproj" -c Release -o /src/build

# Publish stage
FROM build AS publish
ARG PROJECT=SiberVatan

RUN --mount=type=cache,id=nuget-cache,target=/root/.nuget/packages \
    dotnet publish "SiberVatan.csproj" -c Release -o /src/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /src/publish .

# Copy environment file if exists (though handled via docker-compose usually)
# COPY .env . 

ENTRYPOINT ["dotnet", "SiberVatan.dll"]
