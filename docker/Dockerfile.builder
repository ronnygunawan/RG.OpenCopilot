# Multi-stage build for OpenCopilot builder image
# This image includes all build tools required by BuildVerifier across different ecosystems

# Stage 1: Base .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-base

# Stage 2: Build final image with all tools
FROM ubuntu:24.04 AS final

LABEL maintainer="RG.OpenCopilot"
LABEL description="Multi-language builder image for OpenCopilot with .NET, Node.js, Java, Go, and Rust"
LABEL version="1.0.0"

# Prevent interactive prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install base utilities
RUN apt-get update && apt-get install -y \
    git \
    curl \
    wget \
    ca-certificates \
    gnupg \
    lsb-release \
    build-essential \
    unzip \
    && rm -rf /var/lib/apt/lists/*

# Copy .NET SDK from the official image
COPY --from=dotnet-base /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Install Node.js 20 LTS
ARG NODE_VERSION=20.18.1
RUN wget --no-check-certificate https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz -O /tmp/node.tar.xz \
    && tar -xJf /tmp/node.tar.xz -C /usr/local --strip-components=1 \
    && rm /tmp/node.tar.xz

# Install Java 21 (OpenJDK)
RUN apt-get update && apt-get install -y \
    openjdk-21-jdk \
    && rm -rf /var/lib/apt/lists/*

# Set Java environment variables
ENV JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64
ENV PATH="${JAVA_HOME}/bin:${PATH}"

# Install Maven 3.9+
ARG MAVEN_VERSION=3.9.9
RUN wget --no-check-certificate https://archive.apache.org/dist/maven/maven-3/${MAVEN_VERSION}/binaries/apache-maven-${MAVEN_VERSION}-bin.tar.gz -O /tmp/maven.tar.gz \
    && tar -xzf /tmp/maven.tar.gz -C /opt \
    && ln -s /opt/apache-maven-${MAVEN_VERSION} /opt/maven \
    && ln -s /opt/maven/bin/mvn /usr/bin/mvn \
    && rm /tmp/maven.tar.gz

ENV MAVEN_HOME=/opt/maven
ENV PATH="${MAVEN_HOME}/bin:${PATH}"

# Install Go 1.22+
ARG GO_VERSION=1.22.9
RUN wget --no-check-certificate https://go.dev/dl/go${GO_VERSION}.linux-amd64.tar.gz -O /tmp/go.tar.gz \
    && tar -C /usr/local -xzf /tmp/go.tar.gz \
    && rm /tmp/go.tar.gz

ENV GOROOT=/usr/local/go
ENV GOPATH=/go
ENV PATH="${GOROOT}/bin:${GOPATH}/bin:${PATH}"

# Install Rust and Cargo (stable channel)
RUN wget --no-check-certificate -O /tmp/rustup.sh https://sh.rustup.rs \
    && sh /tmp/rustup.sh -y --default-toolchain stable \
    && rm /tmp/rustup.sh
ENV PATH="/root/.cargo/bin:${PATH}"

# Verify all tools are installed with version checks
RUN echo "=== Verifying installed tools ===" \
    && echo "Git version:" && git --version \
    && echo "Curl version:" && curl --version | head -n 1 \
    && echo "Wget version:" && wget --version | head -n 1 \
    && echo ".NET version:" && dotnet --version \
    && echo "Node.js version:" && node --version \
    && echo "npm version:" && npm --version \
    && echo "Java version:" && java -version 2>&1 | head -n 1 \
    && echo "Maven version:" && mvn --version | head -n 1 \
    && echo "Go version:" && go version \
    && echo "Rust version:" && rustc --version \
    && echo "Cargo version:" && cargo --version \
    && echo "=== All tools verified ==="

# Set working directory
WORKDIR /workspace

# Default command
CMD ["/bin/bash"]
