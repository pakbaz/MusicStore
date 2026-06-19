FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MvcMusicStore.csproj ./
RUN dotnet restore MvcMusicStore.csproj

COPY . ./
RUN dotnet publish MvcMusicStore.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "MvcMusicStore.dll"]
