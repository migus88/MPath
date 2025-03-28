#!/bin/bash

# ANSI color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored step messages
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

# Function to check if the user wants to continue
ask_continue() {
    read -p "Continue? (y/n): " choice
    case "$choice" in 
        y|Y ) return 0;;
        * ) print_error "Operation cancelled by user";;
    esac
}

# Step 1: Check if current branch is master
print_step "Checking if current branch is master"
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ "$CURRENT_BRANCH" != "master" ]; then
    print_error "Current branch is $CURRENT_BRANCH, not master. Please switch to master branch."
fi
print_success "Current branch is master"

# Step 2: Check for unstaged or uncommitted changes
print_step "Checking for unstaged or uncommitted changes"
if ! git diff --quiet || ! git diff --cached --quiet; then
    print_error "There are unstaged or uncommitted changes. Please commit or stash them first."
fi
print_success "No unstaged or uncommitted changes found"

# Step 3: Pull the latest code from remote
print_step "Pulling latest code from remote"
if ! git pull; then
    print_error "Failed to pull latest code from remote"
fi
print_success "Successfully pulled latest code"

# Step 4: Ask if version increment is needed
print_step "Do you want to increment the version?"
read -p "Increment version? (y/n): " increment_version
if [[ "$increment_version" =~ ^[Yy]$ ]]; then
    read -p "Which part to increment? [major/minor/patch] (default: minor): " increment_part
    increment_part=${increment_part:-minor}
    
    NEW_VERSION=$(./ci/version-increment.sh "$increment_part")
    if [ $? -ne 0 ]; then
        print_error "Failed to increment version"
    fi
    
    # Extract the version number from the output (last line)
    VERSION=$(echo "$NEW_VERSION" | tail -n1)
    print_success "Version incremented to $VERSION"
else
    # Get current version from package.json
    VERSION=$(grep -o '"version": "[^"]*"' "src/mpath-unity-project/Packages/MPath/package.json" | cut -d'"' -f4)
    print_success "Using existing version: $VERSION"
fi

# Step 5: Check if git tag already exists
print_step "Checking if git tag v$VERSION already exists"
if git rev-parse "v$VERSION" >/dev/null 2>&1; then
    print_error "Tag v$VERSION already exists"
fi
print_success "Tag v$VERSION does not exist yet"

# Step 6: Build the solution
print_step "Building solution"
if ! dotnet build src/mpath-source/Migs.MPath.sln -c Release; then
    print_error "Build failed"
fi
print_success "Build completed successfully"

# Step 7: Run unit tests
print_step "Running unit tests"
if ! dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj -c Release --no-build; then
    print_error "Tests failed"
fi
print_success "All tests passed"

# Step 8: Copy NuGet package to builds directory
print_step "Copying NuGet package to builds directory"
mkdir -p builds

# Find the NuGet package
NUGET_PACKAGE=$(find src/mpath-source/Migs.MPath.Core/bin/Release -name "Migs.MPath.*.nupkg" | sort -V | tail -n1)
if [ -z "$NUGET_PACKAGE" ]; then
    print_error "NuGet package not found"
fi

cp "$NUGET_PACKAGE" builds/
print_success "NuGet package copied to builds directory"

# Step 9: Run Unity in batch mode to create UnityPackage
print_step "Running Unity to create UnityPackage"

# Find Unity on macOS
UNITY_PATH="/Applications/Unity/Hub/Editor"
LATEST_UNITY=$(find "$UNITY_PATH" -maxdepth 1 -type d | sort -V | tail -n1)

if [ -z "$LATEST_UNITY" ]; then
    print_error "Unity installation not found"
fi

UNITY_EXECUTABLE="$LATEST_UNITY/Unity.app/Contents/MacOS/Unity"

if [ ! -f "$UNITY_EXECUTABLE" ]; then
    print_error "Unity executable not found at $UNITY_EXECUTABLE"
fi

UNITY_PROJECT_PATH="$(pwd)/src/mpath-unity-project"

# Run Unity in batch mode
if ! "$UNITY_EXECUTABLE" -batchmode -projectPath "$UNITY_PROJECT_PATH" -executeMethod Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode -quit -logFile unity_build.log; then
    cat unity_build.log
    print_error "Unity batch mode execution failed"
fi

print_success "UnityPackage created successfully"

# Step 10: Create a version tag on master
print_step "Creating version tag v$VERSION"
git tag "v$VERSION"
if ! git push origin "v$VERSION"; then
    print_error "Failed to push tag to remote"
fi
print_success "Tag v$VERSION created and pushed to remote"

echo -e "\n${GREEN}Release process completed successfully!${NC}"
echo "Release artifacts are available in the builds directory:"
ls -la builds/ 