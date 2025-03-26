#!/bin/bash

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <Source DLL Path> <DLL Version>"
  exit 1
fi

# Arguments
SOURCE_DLL="$1"
DLL_VERSION="$2"

# Predefined data
DESTINATION_DLL="./../migs-pathfinding-unity/Packages/Migs.MPath/Migs.MPath.Core.dll"
PACKAGE_JSON="./../migs-pathfinding-unity/Packages/Migs.MPath/package.json"

echo "Starting post-build script..."

# Step 1: Copy the DLL
if [ -f "$SOURCE_DLL" ]; then
  echo "Copying DLL from $SOURCE_DLL to $DESTINATION_DLL"
  cp "$SOURCE_DLL" "$DESTINATION_DLL"
  echo "DLL copied successfully."
else
  echo "Error: Source DLL not found at $SOURCE_DLL."
  exit 1
fi

# Step 2: Update package.json
if [ -f "$PACKAGE_JSON" ]; then
  echo "Updating package.json at $PACKAGE_JSON"
  
  # Use simple sed to update the version field
  sed -i.bak "s/\"version\": \".*\"/\"version\": \"$DLL_VERSION\"/" "$PACKAGE_JSON"
  
  echo "package.json updated successfully with version $DLL_VERSION."
else
  echo "Error: package.json not found at $PACKAGE_JSON."
  exit 1
fi

echo "Post-build script completed successfully."