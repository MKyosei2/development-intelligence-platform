FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY collectors/generic-json-collector/ ./collectors/generic-json-collector/
RUN dotnet publish collectors/generic-json-collector/GenericJsonCollector.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "GenericJsonCollector.dll"]
