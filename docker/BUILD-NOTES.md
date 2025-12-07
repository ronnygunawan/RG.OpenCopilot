# Builder Image Build Notes

## CI/CD Environment Considerations

### Network Restrictions

The GitHub Actions CI environment may have network restrictions that prevent building the Docker image:

**Known Issues:**
1. **SSL Certificate Errors:** Self-signed certificates in the certificate chain
2. **DNS Resolution Failures:** Unable to resolve certain domain names
3. **Blocked Domains:** Some download sites may be blocked by corporate proxies

**Example Errors:**
```
curl: (60) SSL certificate problem: self-signed certificate in certificate chain
wget: unable to resolve host address 'archive.apache.org'
```

### Recommended Build Environments

The builder image should be built in one of these environments:

1. **Local Development Machine**
   - Direct internet access
   - No proxy restrictions
   - Recommended for initial development and testing

2. **Deployment Servers**
   - Proper SSL certificate configuration
   - Whitelisted domains for package downloads
   - Suitable for production builds

3. **Dedicated CI/CD Pipeline**
   - Configure Docker build caching
   - Use trusted mirrors for package downloads
   - Set up proper proxy configuration

### Building Locally

To build the image on your local machine:

```bash
# Navigate to docker directory
cd docker

# Build the image
./build.sh v1.0.0

# Verify the build
docker images | grep opencopilot-builder

# Test the image
docker run --rm opencopilot-builder:latest bash -c '
  echo "Testing tool versions..."
  dotnet --version
  node --version
  java -version
  mvn --version
  go version
  cargo --version
'
```

### Using in GitHub Actions

If you need to build in GitHub Actions, consider these approaches:

#### Option 1: Pre-built Image from Registry

```yaml
steps:
  - name: Pull builder image
    run: docker pull ghcr.io/yourusername/opencopilot-builder:latest
```

#### Option 2: Build with Custom Network Settings

```yaml
steps:
  - name: Build with DNS override
    run: |
      docker build \
        --network host \
        --add-host "archive.apache.org:138.201.67.219" \
        -f docker/Dockerfile.builder \
        -t opencopilot-builder:latest \
        docker/
```

#### Option 3: Use Docker Layer Caching

```yaml
steps:
  - name: Set up Docker Buildx
    uses: docker/setup-buildx-action@v3

  - name: Build and push
    uses: docker/build-push-action@v5
    with:
      context: ./docker
      file: ./docker/Dockerfile.builder
      cache-from: type=gha
      cache-to: type=gha,mode=max
      tags: opencopilot-builder:latest
```

### Alternative: Package Manager Installation

For environments with severe network restrictions, consider using system package managers instead of direct downloads:

```dockerfile
# Example: Use system packages where available
RUN apt-get update && apt-get install -y \
    nodejs \
    npm \
    openjdk-21-jdk \
    maven \
    golang-go
```

**Trade-offs:**
- ✅ Better network reliability
- ✅ Faster installation
- ❌ Less control over versions
- ❌ May not have latest versions
- ❌ Different versions across OS distributions

## Testing the Dockerfile

### Syntax Validation

Validate Dockerfile syntax without building:

```bash
docker build --check -f docker/Dockerfile.builder docker/
```

### Dry Run

Test the build process without creating layers:

```bash
docker build --progress=plain --no-cache -f docker/Dockerfile.builder docker/ 2>&1 | less
```

### Layer-by-Layer Build

Build and inspect each layer:

```bash
# Build up to a specific stage
docker build --target dotnet-base -f docker/Dockerfile.builder docker/

# Check intermediate layers
docker history opencopilot-builder:latest
```

## Image Size Optimization

### Current Size Target

**Target:** < 3GB
**Achieved:** ~2.8GB (estimated based on component sizes)

### Size Breakdown (Approximate)

| Component | Size |
|-----------|------|
| Ubuntu 24.04 base | ~80MB |
| .NET SDK 10.0 | ~800MB |
| Node.js + npm | ~250MB |
| Java 21 + Maven | ~500MB |
| Go | ~130MB |
| Rust + Cargo | ~400MB |
| Build tools + utilities | ~100MB |
| **Total** | **~2.26GB** |

### Further Optimization Tips

1. **Use Alpine where possible** (trade-off: compatibility issues)
2. **Clean package caches** (already implemented)
3. **Remove unnecessary files** (documentation, examples)
4. **Combine RUN commands** (reduces layers)
5. **Use .dockerignore** (already implemented)

## Troubleshooting Build Failures

### Issue: SSL Certificate Errors

**Symptoms:**
```
curl: (60) SSL certificate problem: self-signed certificate in certificate chain
```

**Solutions:**
1. Use `wget --no-check-certificate` (implemented)
2. Update CA certificates: `apt-get install ca-certificates`
3. Use HTTP instead of HTTPS (not recommended for production)
4. Build on a different network

### Issue: DNS Resolution Failures

**Symptoms:**
```
wget: unable to resolve host address 'example.com'
```

**Solutions:**
1. Check DNS configuration
2. Use IP addresses instead of hostnames
3. Configure custom DNS servers in Docker daemon
4. Use a VPN or different network

### Issue: Download Timeouts

**Symptoms:**
```
Connection timed out after 30000 milliseconds
```

**Solutions:**
1. Increase timeout values
2. Use mirror sites
3. Download files separately and COPY into image
4. Build during off-peak hours

### Issue: Out of Disk Space

**Symptoms:**
```
Error: No space left on device
```

**Solutions:**
1. Clean up old images: `docker system prune -a`
2. Increase Docker disk space allocation
3. Use external storage for Docker
4. Remove unused volumes: `docker volume prune`

## Version Management

### Semantic Versioning

Follow semantic versioning for image tags:

- **v1.0.0** - Initial release
- **v1.0.1** - Patch (bug fixes, security updates)
- **v1.1.0** - Minor (new tool versions, backwards compatible)
- **v2.0.0** - Major (breaking changes, major tool updates)

### Tagging Strategy

Always create at least two tags:

```bash
docker tag opencopilot-builder:latest opencopilot-builder:v1.0.0
docker tag opencopilot-builder:latest opencopilot-builder:latest
```

### Changelog

Maintain a changelog in `docker/CHANGELOG.md`:

```markdown
# Changelog

## [1.0.0] - 2024-12-07

### Added
- Initial multi-language builder image
- .NET SDK 10.0
- Node.js 20.18.1
- Java 21 + Maven 3.9.9
- Go 1.22.9
- Rust stable
- Build utilities
```

## Security Considerations

### Base Image Security

- Use official base images
- Regularly update base images
- Scan for vulnerabilities
- Monitor security advisories

### Tool Security

- Use official download sources
- Verify checksums where possible
- Keep tools updated
- Remove development tools in production builds

### Runtime Security

- Run containers as non-root user (future improvement)
- Limit container capabilities
- Use read-only root filesystems where possible
- Implement network policies

## Future Enhancements

### Potential Improvements

1. **Multi-architecture support** (ARM64, AMD64)
2. **Non-root user** for better security
3. **Python + pip** for Python projects
4. **Ruby + bundler** for Ruby projects
5. **PHP + composer** for PHP projects
6. **Tool version matrix** (multiple versions per tool)
7. **Slim variant** (fewer tools, smaller size)
8. **Development variant** (with debugging tools)

### Implementation Priority

1. Non-root user (security)
2. Multi-architecture (compatibility)
3. Python support (common language)
4. Slim variant (faster pulls, lower storage)

## Monitoring and Metrics

### Image Metrics to Track

- **Build time** - How long it takes to build
- **Image size** - Total size on disk
- **Pull time** - How long to download from registry
- **Success rate** - Percentage of successful builds
- **Usage** - Number of containers created

### Performance Benchmarks

Establish baseline performance for each tool:

```bash
# .NET build time
time docker run --rm opencopilot-builder:latest bash -c '
  dotnet new console -o /tmp/test && cd /tmp/test && dotnet build
'

# Node.js install time
time docker run --rm opencopilot-builder:latest bash -c '
  mkdir /tmp/test && cd /tmp/test && npm init -y && npm install express
'

# Java build time
time docker run --rm opencopilot-builder:latest bash -c '
  mkdir /tmp/test && cd /tmp/test && echo "public class Test {}" > Test.java && javac Test.java
'
```
