FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Dip.Worker/Dip.Worker.csproj
RUN dotnet publish src/Dip.Worker/Dip.Worker.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .
RUN useradd --create-home --uid 10001 dipuser
USER dipuser
ENTRYPOINT ["dotnet", "Dip.Worker.dll"]
