FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Review/B2B.Review.Api/B2B.Review.Api.csproj", "src/Services/Review/B2B.Review.Api/"]
COPY ["src/Services/Review/B2B.Review.Application/B2B.Review.Application.csproj", "src/Services/Review/B2B.Review.Application/"]
COPY ["src/Services/Review/B2B.Review.Domain/B2B.Review.Domain.csproj", "src/Services/Review/B2B.Review.Domain/"]
COPY ["src/Services/Review/B2B.Review.Infrastructure/B2B.Review.Infrastructure.csproj", "src/Services/Review/B2B.Review.Infrastructure/"]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Services/Review/B2B.Review.Api/B2B.Review.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Review/B2B.Review.Api"
RUN dotnet build "B2B.Review.Api.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "B2B.Review.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Review.Api.dll"]
