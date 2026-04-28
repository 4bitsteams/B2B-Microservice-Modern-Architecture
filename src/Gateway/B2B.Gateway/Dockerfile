FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/Gateway/B2B.Gateway/B2B.Gateway.csproj", "src/Gateway/B2B.Gateway/"]

RUN dotnet restore "src/Gateway/B2B.Gateway/B2B.Gateway.csproj"

COPY . .
WORKDIR "/src/src/Gateway/B2B.Gateway"
RUN dotnet build "B2B.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "B2B.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Gateway.dll"]
