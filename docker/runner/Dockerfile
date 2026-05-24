FROM ubuntu:24.04

ARG RUNNER_VERSION=2.334.0
ARG TARGETARCH=arm64

ENV DEBIAN_FRONTEND=noninteractive

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

RUN curl -fsSL \
        "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-${TARGETARCH}-${RUNNER_VERSION}.tar.gz" \
        | tar xz \
    && ./bin/installdependencies.sh \
    && chown -R runner:runner /home/runner \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
    && ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

COPY --chmod=755 entrypoint.sh /entrypoint.sh

USER runner

ENTRYPOINT ["/entrypoint.sh"]
