FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Dip.Api/Dip.Api.csproj
RUN dotnet publish src/Dip.Api/Dip.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
RUN useradd --create-home --uid 10001 dipuser \
    && mkdir -p /home/dipuser/.aspnet/DataProtection-Keys \
    && chown -R dipuser:dipuser /home/dipuser /app
ENV HOME=/home/dipuser
USER dipuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Dip.Api.dll"]
