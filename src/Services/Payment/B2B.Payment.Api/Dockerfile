FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Payment/B2B.Payment.Api/B2B.Payment.Api.csproj", "src/Services/Payment/B2B.Payment.Api/"]
COPY ["src/Services/Payment/B2B.Payment.Application/B2B.Payment.Application.csproj", "src/Services/Payment/B2B.Payment.Application/"]
COPY ["src/Services/Payment/B2B.Payment.Domain/B2B.Payment.Domain.csproj", "src/Services/Payment/B2B.Payment.Domain/"]
COPY ["src/Services/Payment/B2B.Payment.Infrastructure/B2B.Payment.Infrastructure.csproj", "src/Services/Payment/B2B.Payment.Infrastructure/"]
COPY ["src/Shared/B2B.Shared.Core/B2B.Shared.Core.csproj", "src/Shared/B2B.Shared.Core/"]
COPY ["src/Shared/B2B.Shared.Infrastructure/B2B.Shared.Infrastructure.csproj", "src/Shared/B2B.Shared.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Services/Payment/B2B.Payment.Api/B2B.Payment.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Payment/B2B.Payment.Api"
RUN dotnet build "B2B.Payment.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "B2B.Payment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "B2B.Payment.Api.dll"]
