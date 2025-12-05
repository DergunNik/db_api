FROM mcr.microsoft.com/dotnet/aspnet:8.0.13 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
EXPOSE 5119

FROM mcr.microsoft.com/dotnet/sdk:8.0.413 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SocNet.Api/SocNet.Api.csproj", "SocNet.Api/"]
RUN dotnet restore "SocNet.Api/SocNet.Api.csproj"
COPY . .
WORKDIR "/src/SocNet.Api"
RUN dotnet build "./SocNet.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SocNet.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SocNet.Api.dll"]
