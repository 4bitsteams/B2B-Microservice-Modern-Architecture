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
COPY ["src/Services/Identity/B2B.Identity.Domain/B2B.Identity.Domain.csproj", "src/Services/Identity/B2B.Identity.Domain/"]
COPY ["src/Services/Identity/B2B.Identity.Application/B2B.Identity.Application.csproj", "src/Services/Identity/B2B.Identity.Application/"]
COPY ["src/Services/Identity/B2B.Identity.Infrastructure/B2B.Identity.Infrastructure.csproj", "src/Services/Identity/B2B.Identity.Infrastructure/"]
COPY ["src/Services/Identity/B2B.Identity.Api/B2B.Identity.Api.csproj", "src/Services/Identity/B2B.Identity.Api/"]

RUN dotnet restore "src/Services/Identity/B2B.Identity.Api/B2B.Identity.Api.csproj"

COPY . .
WORKDIR "/src/src/Services/Identity/B2B.Identity.Api"
RUN dotnet build "B2B.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "B2B.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Identity.Api.dll"]
