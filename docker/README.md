# OpenCopilot Multi-Language Builder Image

This Docker image provides a unified build environment with all development tools required by BuildVerifier to build projects across different ecosystems.

## Included Tools

The `opencopilot-builder` image includes the following build tools:

### .NET
- **Version**: .NET SDK 10.0
- **Purpose**: Build .NET projects (C#, F#, VB.NET)
- **Build command**: `dotnet build`
- **Test command**: `dotnet test`

### Node.js
- **Version**: Node.js 20 LTS + npm (latest)
- **Purpose**: Build JavaScript/TypeScript projects
- **Build command**: `npm run build`
- **Test command**: `npm test`

### Java
- **Version**: OpenJDK 21
- **Purpose**: Build Java projects
- **Gradle**: Supported via Gradle wrapper (`./gradlew`)
- **Maven**: Version 3.9.9
- **Build commands**: 
  - Gradle: `./gradlew build`
  - Maven: `mvn compile`

### Go
- **Version**: Go 1.22.9
- **Purpose**: Build Go projects
- **Build command**: `go build ./...`
- **Test command**: `go test ./...`

### Rust
- **Version**: Stable channel (latest)
- **Purpose**: Build Rust projects with Cargo
- **Build command**: `cargo build`
- **Test command**: `cargo test`

### Utilities
- Git (for repository operations)
- curl, wget (for downloading dependencies)
- bash (shell environment)
- find (for file discovery)
- build-essential (C/C++ compilers and tools)

## Building the Image

### Build Latest Version

```bash
cd docker
./build.sh
```

### Build Specific Version

```bash
cd docker
./build.sh v1.0.0
```

This will tag the image as both `opencopilot-builder:v1.0.0` and `opencopilot-builder:latest`.

### Manual Build

```bash
docker build -f docker/Dockerfile.builder -t opencopilot-builder:latest docker/
```

## Verifying the Image

After building, verify that all tools are installed correctly:

```bash
# .NET
docker run --rm opencopilot-builder:latest dotnet --version

# Node.js
docker run --rm opencopilot-builder:latest node --version
docker run --rm opencopilot-builder:latest npm --version

# Java
docker run --rm opencopilot-builder:latest java -version
docker run --rm opencopilot-builder:latest mvn --version

# Go
docker run --rm opencopilot-builder:latest go version

# Rust
docker run --rm opencopilot-builder:latest rustc --version
docker run --rm opencopilot-builder:latest cargo --version

# Git
docker run --rm opencopilot-builder:latest git --version
```

## Image Size

The target image size is **< 3GB**. Check the actual size with:

```bash
docker images opencopilot-builder:latest
```

The image uses multi-stage builds to minimize size while including all necessary tools.

## Usage in RG.OpenCopilot

The `opencopilot-builder` image is used by:

1. **ContainerManager** - Creates containers for isolated agent task execution
2. **BuildVerifier** - Runs builds and tests across different project types
3. **ExecutorService** - Executes code changes in a safe, isolated environment

To use this image in ContainerManager, update the base image reference in `DockerContainerManager.CreateContainerAsync()`:

```csharp
var result = await _commandExecutor.ExecuteCommandAsync(
    workingDirectory: Directory.GetCurrentDirectory(),
    command: "docker",
    args: new[] {
        "run",
        "-d",
        "--name", containerName,
        "-w", WorkDir,
        "opencopilot-builder:latest",  // Updated from mcr.microsoft.com/dotnet/sdk:10.0
        "sleep", "infinity"
    },
    cancellationToken: cancellationToken);
```

## Publishing to Container Registry

### Tag for Registry

```bash
# For Docker Hub
docker tag opencopilot-builder:latest yourusername/opencopilot-builder:latest
docker tag opencopilot-builder:latest yourusername/opencopilot-builder:v1.0.0

# For GitHub Container Registry
docker tag opencopilot-builder:latest ghcr.io/yourusername/opencopilot-builder:latest
docker tag opencopilot-builder:latest ghcr.io/yourusername/opencopilot-builder:v1.0.0
```

### Push to Registry

```bash
# Docker Hub
docker login
docker push yourusername/opencopilot-builder:latest
docker push yourusername/opencopilot-builder:v1.0.0

# GitHub Container Registry
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
docker push ghcr.io/yourusername/opencopilot-builder:latest
docker push ghcr.io/yourusername/opencopilot-builder:v1.0.0
```

## Updating the Image

### Version Strategy

The image follows semantic versioning:
- `latest` - Always points to the most recent stable build
- `v1.0.0`, `v1.1.0`, etc. - Specific version tags for reproducibility

### When to Update

Update the builder image when:
1. A major version of a build tool is released (e.g., .NET 11, Node.js 22)
2. Security patches are needed for base Ubuntu image
3. New build tools or languages need to be supported
4. Build tool versions need to be updated for compatibility

### Update Process

1. Modify `docker/Dockerfile.builder` with new versions
2. Update version numbers in build arguments (e.g., `MAVEN_VERSION`, `GO_VERSION`)
3. Test the new image locally
4. Build with new version tag: `./build.sh v1.1.0`
5. Verify all tools still work correctly
6. Update this documentation with new version numbers
7. Push to container registry
8. Update `ContainerManager.cs` to reference new image version

### Version History

- **v1.0.0** (Initial release)
  - .NET SDK 10.0
  - Node.js 20 LTS
  - OpenJDK 21
  - Maven 3.9.9
  - Go 1.22.9
  - Rust stable
  - Ubuntu 24.04 LTS base

## Security Considerations

### SSL Certificate Verification

**Note:** The Dockerfile uses `wget --no-check-certificate` for downloading tools due to SSL certificate issues in some CI environments.

**For Production Builds:**
1. Remove `--no-check-certificate` flags
2. Ensure proper CA certificates are installed
3. Use corporate proxy settings if needed
4. Consider using system package managers (apt, snap) instead of direct downloads

**Alternative Secure Download Method:**
```dockerfile
# Update CA certificates first
RUN apt-get update && apt-get install -y ca-certificates

# Download with SSL verification
RUN curl -fsSL https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz \
    -o /tmp/node.tar.xz
```

### Root User

The image runs as root user by default for compatibility with all build tools.

**For Enhanced Security:**
1. Create a non-root user
2. Grant appropriate permissions
3. Switch to non-root user

**Example:**
```dockerfile
# Add after tool installation
RUN useradd -m -s /bin/bash builder \
    && chown -R builder:builder /workspace

USER builder
```

**Trade-offs:**
- ✅ Better security isolation
- ❌ May require additional permission configuration
- ❌ Some build tools expect root access

### Container Security

When running containers from this image:
- Use `--read-only` flag for read-only root filesystem
- Limit container capabilities with `--cap-drop`
- Use security profiles (AppArmor, SELinux)
- Scan image for vulnerabilities regularly

**Example Secure Container Run:**
```bash
docker run \
  --read-only \
  --cap-drop ALL \
  --security-opt no-new-privileges \
  -v /workspace:/workspace:rw \
  opencopilot-builder:latest
```

## Troubleshooting

### Image Too Large

If the image exceeds 3GB:
1. Review and remove unnecessary packages
2. Combine RUN commands to reduce layers
3. Clean up package manager caches (`rm -rf /var/lib/apt/lists/*`)
4. Consider using slimmer base images where possible

### Tool Version Issues

If a specific tool version is needed:
1. Update the corresponding `ARG` in the Dockerfile (e.g., `MAVEN_VERSION`, `GO_VERSION`)
2. For Node.js, change the version in the NodeSource setup URL
3. For Java, install a different JDK package (e.g., `openjdk-17-jdk`)
4. Rebuild the image with `./build.sh`

### Build Failures

If the Docker build fails:
1. Check network connectivity for downloading tools
2. Verify that download URLs are still valid
3. Check for compatibility issues between tools
4. Review the Docker build output for specific error messages

## Architecture Notes

### Multi-Stage Build

The Dockerfile uses a multi-stage build:
1. **Stage 1 (dotnet-base)**: Pulls the official .NET SDK image
2. **Stage 2 (final)**: Builds on Ubuntu 24.04 and copies .NET SDK from stage 1

This approach:
- Reduces final image size by excluding .NET build dependencies
- Ensures official .NET SDK binaries are used
- Allows clean Ubuntu base for other tools

### Environment Variables

The image sets these environment variables:
- `JAVA_HOME`: Java installation directory
- `MAVEN_HOME`: Maven installation directory
- `GOROOT`: Go installation directory
- `GOPATH`: Go workspace directory
- `PATH`: Updated to include all tool binaries

These are automatically available to all processes running in containers.
