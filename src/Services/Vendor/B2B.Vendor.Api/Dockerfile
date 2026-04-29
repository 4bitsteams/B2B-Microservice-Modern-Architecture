FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Vendor/B2B.Vendor.Api/B2B.Vendor.Api.csproj", "src/Services/Vendor/B2B.Vendor.Api/"]
COPY ["src/Services/Vendor/B2B.Vendor.Application/B2B.Vendor.Application.csproj", "src/Services/Vendor/B2B.Vendor.Application/"]
COPY ["src/Services/Vendor/B2B.Vendor.Domain/B2B.Vendor.Domain.csproj", "src/Services/Vendor/B2B.Vendor.Domain/"]
COPY ["src/Services/Vendor/B2B.Vendor.Infrastructure/B2B.Vendor.Infrastructure.csproj", "src/Services/Vendor/B2B.Vendor.Infrastructure/"]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Services/Vendor/B2B.Vendor.Api/B2B.Vendor.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Vendor/B2B.Vendor.Api"
RUN dotnet build "B2B.Vendor.Api.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "B2B.Vendor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Vendor.Api.dll"]
