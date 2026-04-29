FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Discount/B2B.Discount.Api/B2B.Discount.Api.csproj", "src/Services/Discount/B2B.Discount.Api/"]
COPY ["src/Services/Discount/B2B.Discount.Application/B2B.Discount.Application.csproj", "src/Services/Discount/B2B.Discount.Application/"]
COPY ["src/Services/Discount/B2B.Discount.Domain/B2B.Discount.Domain.csproj", "src/Services/Discount/B2B.Discount.Domain/"]
COPY ["src/Services/Discount/B2B.Discount.Infrastructure/B2B.Discount.Infrastructure.csproj", "src/Services/Discount/B2B.Discount.Infrastructure/"]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Services/Discount/B2B.Discount.Api/B2B.Discount.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Discount/B2B.Discount.Api"
RUN dotnet build "B2B.Discount.Api.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "B2B.Discount.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Discount.Api.dll"]
