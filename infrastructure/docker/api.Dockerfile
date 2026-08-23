FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Dip.Api/Dip.Api.csproj
RUN dotnet publish src/Dip.Api/Dip.Api.csproj -c Release -o /out --no-restore

# DIP executes repository QA from the API process. The runtime therefore needs
# the SDK/analyzer toolchain, not only the ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1
ENV PIP_NO_CACHE_DIR=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       bash \
       build-essential \
       ca-certificates \
       clang \
       clang-tidy \
       cmake \
       cppcheck \
       curl \
       git \
       glslang-tools \
       golang-go \
       lua5.4 \
       maven \
       ninja-build \
       nodejs \
       npm \
       openjdk-17-jdk-headless \
       php-cli \
       pkg-config \
       python3 \
       python3-pip \
       python3-venv \
       ruby-full \
       rustc \
       cargo \
       shellcheck \
    && python3 -m venv /opt/dip-qa \
    && /opt/dip-qa/bin/python -m pip install --upgrade pip setuptools wheel \
    && /opt/dip-qa/bin/python -m pip install ruff mypy pytest sqlfluff semgrep \
    && npm install --global typescript eslint \
    && rm -rf /var/lib/apt/lists/* /root/.cache

ENV PATH="/opt/dip-qa/bin:${PATH}"

COPY --from=build /out .

# Docker named volumes start empty. Create/chown every persistent mount point
# in the image first so the non-root API can sync GitHub repositories, restore
# packages and persist QA evidence on the first real-use run.
RUN useradd --create-home --uid 10001 dipuser \
    && mkdir -p \
       /home/dipuser/.aspnet/DataProtection-Keys \
       /home/dipuser/.nuget/packages \
       /home/dipuser/.cache \
       /data/dip-artifacts \
       /data/dip-workspaces \
    && chown -R dipuser:dipuser \
       /home/dipuser \
       /data/dip-artifacts \
       /data/dip-workspaces \
       /app

ENV HOME=/home/dipuser
ENV NUGET_PACKAGES=/home/dipuser/.nuget/packages
USER dipuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Dip.Api.dll"]
