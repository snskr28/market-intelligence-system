FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MarketIntelligence.Console/MarketIntelligence.Console.csproj", "MarketIntelligence.Console/"]
COPY ["src/MarketIntelligence.Core/MarketIntelligence.Core.csproj", "MarketIntelligence.Core/"]
# ... other project references

RUN dotnet restore "MarketIntelligence.Console/MarketIntelligence.Console.csproj"

COPY src/ .
RUN dotnet build "MarketIntelligence.Console/MarketIntelligence.Console.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MarketIntelligence.Console/MarketIntelligence.Console.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/data /app/output

ENTRYPOINT ["dotnet", "MarketIntelligence.Console.dll"]
EOF