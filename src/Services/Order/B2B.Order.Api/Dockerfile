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
COPY ["src/Services/Order/B2B.Order.Domain/B2B.Order.Domain.csproj", "src/Services/Order/B2B.Order.Domain/"]
COPY ["src/Services/Order/B2B.Order.Application/B2B.Order.Application.csproj", "src/Services/Order/B2B.Order.Application/"]
COPY ["src/Services/Order/B2B.Order.Infrastructure/B2B.Order.Infrastructure.csproj", "src/Services/Order/B2B.Order.Infrastructure/"]
COPY ["src/Services/Order/B2B.Order.Api/B2B.Order.Api.csproj", "src/Services/Order/B2B.Order.Api/"]

RUN dotnet restore "src/Services/Order/B2B.Order.Api/B2B.Order.Api.csproj"

COPY . .
WORKDIR "/src/src/Services/Order/B2B.Order.Api"
RUN dotnet build "B2B.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "B2B.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Order.Api.dll"]
