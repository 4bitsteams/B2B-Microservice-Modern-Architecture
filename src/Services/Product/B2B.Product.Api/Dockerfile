FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["src/Services/Product/B2B.Product.Domain/B2B.Product.Domain.csproj", "src/Services/Product/B2B.Product.Domain/"]
COPY ["src/Services/Product/B2B.Product.Application/B2B.Product.Application.csproj", "src/Services/Product/B2B.Product.Application/"]
COPY ["src/Services/Product/B2B.Product.Infrastructure/B2B.Product.Infrastructure.csproj", "src/Services/Product/B2B.Product.Infrastructure/"]
COPY ["src/Services/Product/B2B.Product.Api/B2B.Product.Api.csproj", "src/Services/Product/B2B.Product.Api/"]

RUN dotnet restore "src/Services/Product/B2B.Product.Api/B2B.Product.Api.csproj"

COPY . .
WORKDIR "/src/src/Services/Product/B2B.Product.Api"
RUN dotnet build "B2B.Product.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "B2B.Product.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Product.Api.dll"]
