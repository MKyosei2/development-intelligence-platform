FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Dip.Web/Dip.Web.csproj
RUN dotnet publish src/Dip.Web/Dip.Web.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
RUN useradd --create-home --uid 10001 dipuser \
    && chown -R dipuser:dipuser /app
USER dipuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Dip.Web.dll"]
