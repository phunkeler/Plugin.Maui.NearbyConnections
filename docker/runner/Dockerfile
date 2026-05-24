FROM ubuntu:24.04

ARG RUNNER_VERSION=2.334.0
ARG TARGETARCH=arm64

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_ROOT=/home/runner/.dotnet
ENV DOTNET_INSTALL_DIR=/home/runner/.dotnet
ENV PATH=/home/runner/.dotnet:/home/runner/.dotnet/tools:$PATH

RUN apt-get update && apt-get install -y --no-install-recommends \
        android-tools-adb \
        ca-certificates \
        curl \
        git \
        jq \
        libicu-dev \
        libssl-dev \
        tar \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
        | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
        | tee /etc/apt/sources.list.d/github-cli.list \
    && apt-get update && apt-get install -y --no-install-recommends gh \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -m -s /bin/bash runner \
    && usermod -aG plugdev runner

WORKDIR /home/runner

ADD https://dot.net/v1/dotnet-install.sh /tmp/dotnet-install.sh
COPY global.json /tmp/global.json

RUN curl -fsSL \
        "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-${TARGETARCH}-${RUNNER_VERSION}.tar.gz" \
        | tar xz \
    && ./bin/installdependencies.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && DOTNET_VERSION=$(jq -r '.sdk.version' /tmp/global.json) \
    && /tmp/dotnet-install.sh --version "$DOTNET_VERSION" --install-dir /home/runner/.dotnet \
    && chown -R runner:runner /home/runner \
    && rm /tmp/dotnet-install.sh /tmp/global.json

COPY --chmod=755 docker/runner/entrypoint.sh /entrypoint.sh

USER runner

RUN dotnet tool install --global Microsoft.DotNet.XHarness.CLI --prerelease

ENTRYPOINT ["/entrypoint.sh"]
