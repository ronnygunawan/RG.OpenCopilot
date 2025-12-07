#!/bin/bash
# Build script for OpenCopilot builder image

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKERFILE="${SCRIPT_DIR}/Dockerfile.builder"
IMAGE_NAME="opencopilot-builder"
VERSION="${1:-latest}"

echo "Building OpenCopilot builder image..."
echo "Image name: ${IMAGE_NAME}"
echo "Version: ${VERSION}"
echo "Dockerfile: ${DOCKERFILE}"

# Build the image
docker build \
    -f "${DOCKERFILE}" \
    -t "${IMAGE_NAME}:${VERSION}" \
    -t "${IMAGE_NAME}:latest" \
    "${SCRIPT_DIR}"

# Get image size
IMAGE_SIZE=$(docker images "${IMAGE_NAME}:${VERSION}" --format "{{.Size}}")
echo ""
echo "Build completed successfully!"
echo "Image: ${IMAGE_NAME}:${VERSION}"
echo "Size: ${IMAGE_SIZE}"
echo ""
echo "To verify tools, run:"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} dotnet --version"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} node --version"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} java -version"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} mvn --version"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} go version"
echo "  docker run --rm ${IMAGE_NAME}:${VERSION} cargo --version"
