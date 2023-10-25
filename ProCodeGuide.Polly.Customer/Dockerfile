FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ProCodeGuide.Polly.Customer.csproj", "src/"]
RUN dotnet restore "src/ProCodeGuide.Polly.Customer.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "ProCodeGuide.Polly.Customer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProCodeGuide.Polly.Customer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProCodeGuide.Polly.Customer.dll"]