FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Shipping/B2B.Shipping.Api/B2B.Shipping.Api.csproj", "src/Services/Shipping/B2B.Shipping.Api/"]
COPY ["src/Services/Shipping/B2B.Shipping.Application/B2B.Shipping.Application.csproj", "src/Services/Shipping/B2B.Shipping.Application/"]
COPY ["src/Services/Shipping/B2B.Shipping.Domain/B2B.Shipping.Domain.csproj", "src/Services/Shipping/B2B.Shipping.Domain/"]
COPY ["src/Services/Shipping/B2B.Shipping.Infrastructure/B2B.Shipping.Infrastructure.csproj", "src/Services/Shipping/B2B.Shipping.Infrastructure/"]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Services/Shipping/B2B.Shipping.Api/B2B.Shipping.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Shipping/B2B.Shipping.Api"
RUN dotnet build "B2B.Shipping.Api.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "B2B.Shipping.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Shipping.Api.dll"]
