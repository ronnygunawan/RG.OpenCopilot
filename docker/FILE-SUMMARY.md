# Multi-Language Builder Image - File Summary

This document provides a quick reference for all files related to the multi-language builder Docker image.

## Docker Image Files

### `/docker/Dockerfile.builder`
The main Dockerfile that defines the multi-language builder image.

**Contents:**
- Multi-stage build starting from Ubuntu 24.04 and .NET SDK 10.0
- Installation of Node.js, Java, Maven, Go, and Rust
- Environment variable configuration
- Tool version verification

**Key Features:**
- Optimized with multi-stage builds
- All tools verified during build
- Clean package manager caches for smaller size
- Target size: < 3GB

### `/docker/build.sh`
Build script for creating the Docker image.

**Usage:**
```bash
./docker/build.sh           # Builds as :latest
./docker/build.sh v1.0.0    # Builds as :v1.0.0 and :latest
```

**Output:**
- Creates tagged images
- Displays image size
- Shows verification commands

### `/docker/.dockerignore`
Excludes unnecessary files from Docker build context.

**Ignored:**
- Markdown documentation
- Build scripts
- Git files

## Documentation Files

### `/docker/README.md`
Comprehensive documentation for the builder image.

**Sections:**
- Tool versions and purposes
- Building instructions
- Verification procedures
- Usage in RG.OpenCopilot
- Publishing to registries
- Version management
- Troubleshooting
- Maintenance guidelines

### `/docker/INTEGRATION.md`
Detailed integration guide for developers.

**Sections:**
- Build process step-by-step
- Tool verification commands
- Build capability testing for each language
- Integration with RG.OpenCopilot codebase
- Registry publishing examples
- CI/CD integration samples
- Troubleshooting common issues
- Maintenance procedures

## Code Changes

### Updated Files

#### `RG.OpenCopilot.PRGenerationAgent.Services/Docker/ContainerManager.cs`
**Change:** Updated `CreateContainerAsync()` method
- Changed base image from `mcr.microsoft.com/dotnet/sdk:10.0` to `opencopilot-builder:latest`
- Removed git installation steps (already in builder image)
- Updated comments to reflect multi-language support

**Impact:**
- Containers now have all build tools pre-installed
- Faster container startup (no apt-get updates needed)
- Support for Java, Go, and Rust projects

#### `RG.OpenCopilot.Tests/DirectoryOperationsIntegrationTests.cs`
**Change:** Updated test container image
- Changed from `mcr.microsoft.com/dotnet/sdk:10.0` to `opencopilot-builder:latest`

**Impact:**
- Integration tests use same image as production
- Tests verify multi-language tooling is available

#### `RG.OpenCopilot.Tests/FileEditorIntegrationTests.cs`
**Change:** Updated test container image
- Changed from `mcr.microsoft.com/dotnet/sdk:10.0` to `opencopilot-builder:latest`
- Updated comment to reflect multi-language builder

**Impact:**
- Consistent testing environment
- File editor tests run in production-like container

#### `README.md`
**Change:** Added "Multi-Language Builder Image" section
- Documents the builder image and its tools
- Links to docker/README.md and docker/INTEGRATION.md
- Updated Container-Based Executor feature list

**Impact:**
- Users understand the multi-language capabilities
- Clear documentation path for setup

## Quick Start

### For Users

1. **Build the image:**
   ```bash
   cd docker
   ./build.sh v1.0.0
   ```

2. **Verify tools:**
   ```bash
   docker run --rm opencopilot-builder:latest dotnet --version
   docker run --rm opencopilot-builder:latest node --version
   docker run --rm opencopilot-builder:latest java -version
   docker run --rm opencopilot-builder:latest go version
   docker run --rm opencopilot-builder:latest cargo --version
   ```

3. **Run tests:**
   ```bash
   dotnet test --filter "FullyQualifiedName!~IntegrationTests"  # Unit tests only
   dotnet test  # All tests (requires builder image)
   ```

### For Developers

1. **Read the integration guide:**
   - See `docker/INTEGRATION.md` for detailed instructions

2. **Build and test locally:**
   - Build image: `cd docker && ./build.sh`
   - Run builds: `dotnet build`
   - Run tests: `dotnet test`

3. **Publish to registry (optional):**
   - Tag for your registry
   - Push using docker push
   - Update ContainerManager.cs with registry path

## Tool Versions

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0 | C#, F#, VB.NET projects |
| Node.js | 20.18.1 (LTS) | JavaScript/TypeScript |
| npm | 10.x (bundled) | Node package management |
| Java (OpenJDK) | 21 | Java projects |
| Maven | 3.9.9 | Java build tool |
| Gradle | Via wrapper | Java build tool |
| Go | 1.22.9 | Go projects |
| Rust | stable | Rust projects |
| Cargo | stable | Rust package management |

## Architecture Notes

### Multi-Stage Build

The Dockerfile uses a two-stage build:

1. **Stage 1 (dotnet-base):** Pulls official .NET SDK 10.0 image
2. **Stage 2 (final):** Ubuntu 24.04 base + copies .NET + installs other tools

**Benefits:**
- Reduces final image size
- Uses official .NET binaries
- Clean Ubuntu base for other tools

### Environment Variables

Set in the image:
- `JAVA_HOME`: Java installation path
- `MAVEN_HOME`: Maven installation path
- `GOROOT`: Go installation path
- `GOPATH`: Go workspace path
- `PATH`: Updated with all tool binaries

### Build Tool Detection

BuildVerifier automatically detects projects by searching for:
- .NET: `*.csproj`, `*.fsproj`, `*.vbproj`
- Node.js: `package.json`
- Java/Gradle: `build.gradle`, `build.gradle.kts`
- Java/Maven: `pom.xml`
- Go: `go.mod`
- Rust: `Cargo.toml`

## Maintenance

### Updating Tool Versions

1. Edit `docker/Dockerfile.builder`
2. Update `ARG` values (NODE_VERSION, MAVEN_VERSION, GO_VERSION)
3. Rebuild: `./build.sh v1.x.0`
4. Test thoroughly
5. Update documentation
6. Tag and push to registry

### Security Updates

- Rebuild monthly for Ubuntu and tool updates
- Monitor security advisories for:
  - Ubuntu 24.04
  - .NET SDK
  - OpenJDK
  - Node.js
  - Other tools

## Support

For issues or questions:
1. Check `docker/README.md`
2. Check `docker/INTEGRATION.md`
3. Review BuildVerifier.cs for build detection logic
4. Open GitHub issue with:
   - Docker version
   - Operating system
   - Error messages
   - Steps to reproduce
