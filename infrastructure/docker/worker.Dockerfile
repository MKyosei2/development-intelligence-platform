FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Dip.Worker/Dip.Worker.csproj
RUN dotnet publish src/Dip.Worker/Dip.Worker.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .

RUN useradd --create-home --uid 10001 dipuser \
    && mkdir -p \
       /data/dip-artifacts \
       /data/dip-workspaces \
    && chown -R dipuser:dipuser \
       /data/dip-artifacts \
       /data/dip-workspaces \
       /app

ENV HOME=/home/dipuser
USER dipuser
EXPOSE 9091
ENTRYPOINT ["dotnet", "Dip.Worker.dll"]
