#!/bin/bash

# Default to minor version increment if not specified
VERSION_PART=${1:-minor}

# Validate arguments
if [[ ! "$VERSION_PART" =~ ^(major|minor|patch)$ ]]; then
    echo "Error: Version part must be one of [major, minor, patch]"
    echo "Usage: ./version-increment.sh [major|minor|patch]"
    exit 1
fi

# File paths
CSPROJ_PATH="src/mpath-source/Migs.MPath.Core/Migs.MPath.Core.csproj"
PACKAGE_JSON_PATH="src/mpath-unity-project/Packages/MPath/package.json"

# Check if files exist
if [ ! -f "$CSPROJ_PATH" ]; then
    echo "Error: $CSPROJ_PATH not found"
    exit 1
fi

if [ ! -f "$PACKAGE_JSON_PATH" ]; then
    echo "Error: $PACKAGE_JSON_PATH not found"
    exit 1
fi

# Extract current version from package.json
CURRENT_VERSION=$(grep -o '"version": "[^"]*"' "$PACKAGE_JSON_PATH" | cut -d'"' -f4)

if [ -z "$CURRENT_VERSION" ]; then
    echo "Error: Could not extract version from $PACKAGE_JSON_PATH"
    exit 1
fi

# Split version into components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Increment the appropriate part
case $VERSION_PART in
    major)
        MAJOR=$((MAJOR+1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR+1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH+1))
        ;;
esac

# Create new version string
NEW_VERSION="$MAJOR.$MINOR.$PATCH"

echo "Incrementing $VERSION_PART version: $CURRENT_VERSION -> $NEW_VERSION"

# Update package.json
sed -i "" "s/\"version\": \"$CURRENT_VERSION\"/\"version\": \"$NEW_VERSION\"/" "$PACKAGE_JSON_PATH"

# Update version in .csproj file (handles both Version and AssemblyVersion)
sed -i "" "s/<Version>$CURRENT_VERSION<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$CSPROJ_PATH"
sed -i "" "s/<AssemblyVersion>$CURRENT_VERSION<\/AssemblyVersion>/<AssemblyVersion>$NEW_VERSION<\/AssemblyVersion>/" "$CSPROJ_PATH"

echo "Version updated to $NEW_VERSION in both files"
echo "$NEW_VERSION" 