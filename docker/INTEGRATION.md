# Multi-Language Builder Image Integration Guide

This guide explains how to build, test, and integrate the `opencopilot-builder` Docker image with RG.OpenCopilot.

## Overview

The `opencopilot-builder` image is a unified Docker image that includes all build tools required by BuildVerifier to compile and test projects across different ecosystems:

- .NET SDK 10.0
- Node.js 20 LTS + npm
- Java 21 (OpenJDK) + Maven 3.9+ + Gradle wrapper support
- Go 1.22+
- Rust + Cargo (stable)
- Utilities: git, curl, wget, bash, find, build-essential

## Building the Image

### Prerequisites

- Docker installed and running
- Internet connection for downloading build tools
- At least 5GB of free disk space

### Build Steps

1. **Navigate to the docker directory:**
   ```bash
   cd docker
   ```

2. **Build using the build script:**
   ```bash
   ./build.sh v1.0.0
   ```

   This creates two tags:
   - `opencopilot-builder:v1.0.0`
   - `opencopilot-builder:latest`

3. **Verify the build:**
   ```bash
   docker images opencopilot-builder
   ```

   Expected output:
   ```
   REPOSITORY            TAG       IMAGE ID       CREATED         SIZE
   opencopilot-builder   latest    <id>          <time>          <2.8GB
   opencopilot-builder   v1.0.0    <id>          <time>          <2.8GB
   ```

### Manual Build

If you prefer to build manually:

```bash
docker build \
  -f docker/Dockerfile.builder \
  -t opencopilot-builder:latest \
  -t opencopilot-builder:v1.0.0 \
  docker/
```

## Verifying Tool Installation

After building the image, verify all tools are correctly installed:

### Quick Verification

Run the verification commands built into the image:

```bash
docker run --rm opencopilot-builder:latest bash -c '
  echo "=== Tool Versions ==="
  echo "Git: $(git --version)"
  echo ".NET: $(dotnet --version)"
  echo "Node.js: $(node --version)"
  echo "npm: $(npm --version)"
  echo "Java: $(java -version 2>&1 | head -n 1)"
  echo "Maven: $(mvn --version | head -n 1)"
  echo "Go: $(go version)"
  echo "Rust: $(rustc --version)"
  echo "Cargo: $(cargo --version)"
'
```

### Individual Tool Checks

```bash
# .NET
docker run --rm opencopilot-builder:latest dotnet --version
# Expected: 10.0.x

# Node.js
docker run --rm opencopilot-builder:latest node --version
# Expected: v20.18.1

# npm
docker run --rm opencopilot-builder:latest npm --version
# Expected: 10.x.x

# Java
docker run --rm opencopilot-builder:latest java -version
# Expected: openjdk version "21.0.x"

# Maven
docker run --rm opencopilot-builder:latest mvn --version
# Expected: Apache Maven 3.9.9

# Go
docker run --rm opencopilot-builder:latest go version
# Expected: go version go1.22.9 linux/amd64

# Rust
docker run --rm opencopilot-builder:latest rustc --version
# Expected: rustc 1.x.x (stable)

# Cargo
docker run --rm opencopilot-builder:latest cargo --version
# Expected: cargo 1.x.x
```

## Testing Build Capabilities

### Test .NET Build

```bash
# Create and build a .NET project
docker run --rm -v $(pwd):/workspace -w /workspace opencopilot-builder:latest bash -c '
  dotnet new console -n TestApp -o /tmp/test
  cd /tmp/test
  dotnet build
'
```

### Test Node.js Build

```bash
# Create and build a Node.js project
docker run --rm -v $(pwd):/workspace -w /workspace opencopilot-builder:latest bash -c '
  mkdir -p /tmp/test && cd /tmp/test
  npm init -y
  npm install express --save
'
```

### Test Java Build with Maven

```bash
# Create and build a Maven project
docker run --rm -v $(pwd):/workspace -w /workspace opencopilot-builder:latest bash -c '
  cd /tmp
  mvn archetype:generate -DgroupId=com.test -DartifactId=test-app \
    -DarchetypeArtifactId=maven-archetype-quickstart -DinteractiveMode=false
  cd test-app
  mvn compile
'
```

### Test Go Build

```bash
# Create and build a Go project
docker run --rm -v $(pwd):/workspace -w /workspace opencopilot-builder:latest bash -c '
  mkdir -p /tmp/test && cd /tmp/test
  go mod init example.com/hello
  echo "package main\nimport \"fmt\"\nfunc main() { fmt.Println(\"Hello\") }" > main.go
  go build
'
```

### Test Rust Build

```bash
# Create and build a Rust project
docker run --rm -v $(pwd):/workspace -w /workspace opencopilot-builder:latest bash -c '
  cd /tmp
  cargo new test_project
  cd test_project
  cargo build
'
```

## Integration with RG.OpenCopilot

### Code Changes

The following files have been updated to use the new builder image:

1. **ContainerManager.cs** - Updated `CreateContainerAsync()` to use `opencopilot-builder:latest` instead of `mcr.microsoft.com/dotnet/sdk:10.0`
2. **DirectoryOperationsIntegrationTests.cs** - Updated test container image
3. **FileEditorIntegrationTests.cs** - Updated test container image

### Before Using in Production

1. **Build the image locally or in CI/CD:**
   ```bash
   cd docker
   ./build.sh v1.0.0
   ```

2. **Verify the image is available:**
   ```bash
   docker images | grep opencopilot-builder
   ```

3. **Run unit tests (no Docker required):**
   ```bash
   dotnet test --filter "FullyQualifiedName!~IntegrationTests"
   ```

4. **Run integration tests (requires Docker with builder image):**
   ```bash
   dotnet test
   ```

### Using a Different Image Name

If you need to use a different image name or tag:

1. **Update the image reference in ContainerManager.cs:**
   ```csharp
   // Change from:
   "opencopilot-builder:latest",
   
   // To your custom image:
   "your-registry/opencopilot-builder:v1.0.0",
   ```

2. **Update test files similarly** (DirectoryOperationsIntegrationTests.cs, FileEditorIntegrationTests.cs)

## Publishing to Registry

### Docker Hub

```bash
# Tag for Docker Hub
docker tag opencopilot-builder:latest yourusername/opencopilot-builder:latest
docker tag opencopilot-builder:latest yourusername/opencopilot-builder:v1.0.0

# Login and push
docker login
docker push yourusername/opencopilot-builder:latest
docker push yourusername/opencopilot-builder:v1.0.0
```

### GitHub Container Registry (GHCR)

```bash
# Tag for GHCR
docker tag opencopilot-builder:latest ghcr.io/ronnygunawan/opencopilot-builder:latest
docker tag opencopilot-builder:latest ghcr.io/ronnygunawan/opencopilot-builder:v1.0.0

# Login and push
echo $GITHUB_TOKEN | docker login ghcr.io -u ronnygunawan --password-stdin
docker push ghcr.io/ronnygunawan/opencopilot-builder:latest
docker push ghcr.io/ronnygunawan/opencopilot-builder:v1.0.0
```

### Azure Container Registry (ACR)

```bash
# Tag for ACR
docker tag opencopilot-builder:latest youracr.azurecr.io/opencopilot-builder:latest
docker tag opencopilot-builder:latest youracr.azurecr.io/opencopilot-builder:v1.0.0

# Login and push
az acr login --name youracr
docker push youracr.azurecr.io/opencopilot-builder:latest
docker push youracr.azurecr.io/opencopilot-builder:v1.0.0
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Push Builder Image

on:
  push:
    branches: [ main ]
    paths:
      - 'docker/**'

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - uses: actions/checkout@v4

      - name: Login to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: ./docker
          file: ./docker/Dockerfile.builder
          push: true
          tags: |
            ghcr.io/${{ github.repository }}/opencopilot-builder:latest
            ghcr.io/${{ github.repository }}/opencopilot-builder:${{ github.sha }}
```

## Troubleshooting

### Image Build Fails

**Problem:** Build fails with network errors when downloading tools.

**Solution:**
- Check internet connection
- Verify firewall/proxy settings
- Some corporate networks may block certain download sites
- Consider using a VPN or building on a different network

### Image Size Too Large

**Problem:** Final image exceeds 3GB.

**Solution:**
- Review Dockerfile and ensure package manager caches are cleaned
- Consider using Alpine-based images where possible
- Remove unnecessary build dependencies after installation
- Use multi-stage builds more aggressively

### Tool Version Conflicts

**Problem:** Specific version of a tool is needed.

**Solution:**
Update the corresponding `ARG` in the Dockerfile:
```dockerfile
ARG MAVEN_VERSION=3.9.10  # Update version here
ARG GO_VERSION=1.22.10    # Update version here
ARG NODE_VERSION=20.19.0  # Update version here
```

### Container Fails to Start

**Problem:** Container created by ContainerManager fails to start.

**Solution:**
1. Verify image exists: `docker images | grep opencopilot-builder`
2. Test image manually: `docker run --rm opencopilot-builder:latest echo "test"`
3. Check Docker daemon logs for errors
4. Ensure sufficient disk space and memory

## Maintenance

### Updating Tool Versions

1. Edit `docker/Dockerfile.builder`
2. Update version ARGs:
   - `NODE_VERSION`
   - `MAVEN_VERSION`
   - `GO_VERSION`
3. Rebuild with new version: `./build.sh v1.1.0`
4. Test thoroughly before deploying
5. Update documentation with new versions

### Security Updates

Regularly rebuild the image to get security updates from:
- Ubuntu base image
- .NET SDK
- OpenJDK
- Other system packages

Recommended: Rebuild at least monthly or when critical vulnerabilities are announced.

## Support

For issues related to the builder image:
1. Check this integration guide
2. Review the main [README.md](../docker/README.md)
3. Open an issue on the GitHub repository
4. Include Docker version, OS, and error messages
