#!/bin/bash

# Script to run Unity to export a package, then modify its contents to change asset paths

# Check if debug mode is enabled
DEBUG_MODE=false
if [ "$1" == "debug" ]; then
    DEBUG_MODE=true
    echo "Debug mode enabled - verbose logging will be shown"
fi

# ANSI color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored messages
print_step() {
    echo -e "${YELLOW}STEP: $1${NC}"
}

# Function to print success messages
print_success() {
    echo -e "${GREEN}SUCCESS: $1${NC}"
}

# Function to print error messages and exit
print_error() {
    echo -e "${RED}ERROR: $1${NC}"
    exit 1
}

# Function to print debug info only if debug mode is enabled
print_debug() {
    if [ "$DEBUG_MODE" = true ]; then
        echo -e "${BLUE}DEBUG: $1${NC}"
    fi
}

# Paths
BUILDS_DIR="builds"
UNITY_PROJECT_PATH="$(pwd)/src/mpath-unity-project"
TEMP_DIR="$(pwd)/tmp_package"
UNITY_LOG="$(pwd)/unity_build.log"

# Create builds directory if it doesn't exist
mkdir -p "$BUILDS_DIR"
print_debug "Created builds directory: $BUILDS_DIR"

# Step 1: Run Unity to create the package
print_step "Running Unity to create UnityPackage"

# Find Unity on macOS
UNITY_PATH="/Applications/Unity/Hub/Editor"
print_debug "Looking for Unity in: $UNITY_PATH"

# List all Unity versions found
if [ "$DEBUG_MODE" = true ]; then
    echo "Unity versions found:"
    find "$UNITY_PATH" -maxdepth 1 -type d | sort -V | while read unity_ver; do
        echo "  - $(basename "$unity_ver")"
    done
fi

LATEST_UNITY=$(find "$UNITY_PATH" -maxdepth 1 -type d | sort -V | tail -n1)

if [ -z "$LATEST_UNITY" ]; then
    print_error "Unity installation not found in $UNITY_PATH"
fi

print_debug "Using Unity at: $LATEST_UNITY"
UNITY_EXECUTABLE="$LATEST_UNITY/Unity.app/Contents/MacOS/Unity"

if [ ! -f "$UNITY_EXECUTABLE" ]; then
    print_error "Unity executable not found at $UNITY_EXECUTABLE"
fi

# Check if project path exists
if [ ! -d "$UNITY_PROJECT_PATH" ]; then
    print_error "Unity project path does not exist: $UNITY_PROJECT_PATH"
fi
print_debug "Unity project path: $UNITY_PROJECT_PATH"

# Check if Editor script exists
EDITOR_SCRIPT_PATH="$UNITY_PROJECT_PATH/Assets/Editor/PackageExporter.cs"
if [ ! -f "$EDITOR_SCRIPT_PATH" ]; then
    print_error "Editor script not found at $EDITOR_SCRIPT_PATH"
fi
print_debug "Editor script exists at: $EDITOR_SCRIPT_PATH"

# Clean up any previous logs
rm -f "$UNITY_LOG" 2>/dev/null

# Run Unity in batch mode to create the package
print_step "Running Unity in batch mode..."
print_debug "Command: $UNITY_EXECUTABLE -batchmode -projectPath $UNITY_PROJECT_PATH -executeMethod Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode -logFile $UNITY_LOG -quit"

"$UNITY_EXECUTABLE" -batchmode -projectPath "$UNITY_PROJECT_PATH" -executeMethod Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode -logFile "$UNITY_LOG" -quit

UNITY_EXIT_CODE=$?
print_debug "Unity exit code: $UNITY_EXIT_CODE"

# Always show the log if in debug mode or if there was an error
if [ "$DEBUG_MODE" = true ] || [ $UNITY_EXIT_CODE -ne 0 ]; then
    if [ -f "$UNITY_LOG" ]; then
        echo "=== Unity Log ==="
        cat "$UNITY_LOG"
        echo "=== End Unity Log ==="
    else
        print_debug "No Unity log file was created!"
    fi
fi

# Check log for common errors even if exit code is 0
if [ -f "$UNITY_LOG" ]; then
    if grep -q "ExecuteMethod method Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode not found" "$UNITY_LOG"; then
        print_error "Method not found: Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode"
    fi
    
    if grep -q "Assembly 'Assembly-CSharp-Editor' will not be loaded due to errors:" "$UNITY_LOG"; then
        print_error "Editor script has compilation errors"
    fi
fi

if [ $UNITY_EXIT_CODE -ne 0 ]; then
    print_error "Unity batch mode execution failed with exit code $UNITY_EXIT_CODE"
fi

print_success "Unity batch execution completed"

# Check if any packages were created
print_debug "Checking for created packages in $BUILDS_DIR"
if [ "$DEBUG_MODE" = true ]; then
    find "$BUILDS_DIR" -name "*.unitypackage" -type f -print
fi

# Find the generated package
UNITY_PACKAGE=$(find "$BUILDS_DIR" -name "migs-mpath-*.unitypackage" | sort -V | tail -n1)

if [ -z "$UNITY_PACKAGE" ]; then
    print_error "UnityPackage not found in $BUILDS_DIR. Package generation failed."
fi

print_debug "Found package: $UNITY_PACKAGE"
print_debug "Package file size: $(du -h "$UNITY_PACKAGE" | cut -f1)"

VERSION=$(echo "$UNITY_PACKAGE" | grep -o '[0-9]\+\.[0-9]\+\.[0-9]\+' | head -1)
if [ -z "$VERSION" ]; then
    print_error "Could not extract version from package filename"
fi

print_success "Found UnityPackage: $UNITY_PACKAGE (version $VERSION)"

# Step 2: Extract the Unity package
print_step "Extracting Unity package..."

# Create temporary directory
rm -rf "$TEMP_DIR" 2>/dev/null
mkdir -p "$TEMP_DIR"
print_debug "Created temp directory: $TEMP_DIR"

# Extract package to temp directory
print_debug "Extracting with: tar -xzf \"$UNITY_PACKAGE\" -C \"$TEMP_DIR\""
tar -xzf "$UNITY_PACKAGE" -C "$TEMP_DIR"

if [ $? -ne 0 ]; then
    print_error "Failed to extract Unity package. Check if the file is a valid tar.gz archive."
fi

# Count extracted items
EXTRACTED_COUNT=$(find "$TEMP_DIR" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')
print_debug "Number of GUID directories extracted: $EXTRACTED_COUNT"

if [ "$EXTRACTED_COUNT" -eq 0 ]; then
    print_error "No GUID directories found in the package. Either the package is empty or not a valid Unity package."
fi

print_success "Package extracted to $TEMP_DIR ($EXTRACTED_COUNT items)"

# Step 3: Modify the pathname files
print_step "Modifying pathname files to change asset paths..."

# Find all directories in the temp dir - these are the GUIDs
MODIFIED_COUNT=0
find "$TEMP_DIR" -mindepth 1 -maxdepth 1 -type d | while read guid_dir; do
    PATHNAME_FILE="$guid_dir/pathname"
    
    if [ -f "$PATHNAME_FILE" ]; then
        # Read current path
        CURRENT_PATH=$(cat "$PATHNAME_FILE")
        
        # Check if path starts with Assets/
        if [[ "$CURRENT_PATH" == Assets/* ]]; then
            # Change Assets/ to Packages/ (without escape characters)
            NEW_PATH="Packages/${CURRENT_PATH#Assets/}"
            
            # Write new path back to file
            echo "$NEW_PATH" > "$PATHNAME_FILE"
            if [ "$DEBUG_MODE" = true ]; then
                echo "Changed: $CURRENT_PATH -> $NEW_PATH"
            fi
            MODIFIED_COUNT=$((MODIFIED_COUNT + 1))
        else
            print_debug "Skipping non-Assets path: $CURRENT_PATH"
        fi
    else
        print_debug "No pathname file in $(basename "$guid_dir")"
    fi
done

print_debug "Modified $MODIFIED_COUNT pathname files"
print_success "All pathname files processed"

# Step 4: Create new archive
print_step "Creating new archive..."

NEW_PACKAGE="${UNITY_PACKAGE%.unitypackage}.modified.tar.gz"
print_debug "Creating new package: $NEW_PACKAGE"

# Create tarball from the temp directory
tar -czf "$NEW_PACKAGE" -C "$TEMP_DIR" .

if [ $? -ne 0 ]; then
    print_error "Failed to create new archive"
fi

print_debug "New package size: $(du -h "$NEW_PACKAGE" | cut -f1)"
print_success "New archive created: $NEW_PACKAGE"

# Step 5: Replace original file
print_step "Replacing original unity package..."

# Replace with our modified version (no backup)
mv "$NEW_PACKAGE" "$UNITY_PACKAGE"

if [ $? -ne 0 ]; then
    print_error "Failed to rename modified package"
fi

print_success "Original package replaced with modified version"

# Cleanup
print_step "Cleaning up..."
rm -rf "$TEMP_DIR"
print_success "Temporary files removed"

print_success "Package processing completed successfully"
echo "Final package: $UNITY_PACKAGE" 