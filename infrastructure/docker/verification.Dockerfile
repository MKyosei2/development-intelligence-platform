FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1
ENV NUGET_XMLDOC_MODE=skip

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
       postgresql-client \
       python3 \
       python3-pip \
       python3-venv \
       ruby-full \
       rustc \
       cargo \
       shellcheck \
    && python3 -m venv /opt/dip-qa \
    && /opt/dip-qa/bin/python -m pip install --upgrade pip setuptools wheel \
    && /opt/dip-qa/bin/python -m pip install ruff mypy pytest sqlfluff semgrep pip-audit \
    && npm install --global typescript eslint \
    && rm -rf /var/lib/apt/lists/* /root/.cache

ENV PATH="/opt/dip-qa/bin:${PATH}"
WORKDIR /work

ENTRYPOINT ["bash", "-lc"]
