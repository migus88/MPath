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

# Step 1: Check current branch
print_step "Checking current branch"
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ "$CURRENT_BRANCH" != "master" ]; then
    echo -e "${YELLOW}Warning: Current branch is $CURRENT_BRANCH, not master.${NC}"
    echo "It's recommended to run this script on the master branch."
    read -p "Continue on $CURRENT_BRANCH branch? (y/n): " continue_nonmaster
    if [[ ! "$continue_nonmaster" =~ ^[Yy]$ ]]; then
        print_error "Operation cancelled by user. Please switch to master branch."
    fi
    print_success "Continuing on $CURRENT_BRANCH branch"
else
    print_success "Current branch is master"
fi

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
print_step "Version increment"
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
print_step "Checking if git tag $VERSION already exists"
if git rev-parse "$VERSION" >/dev/null 2>&1; then
    print_error "Tag $VERSION already exists"
fi
print_success "Tag $VERSION does not exist yet"

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

# Step 9: Create package tarball with modified paths
print_step "Creating Unity package with proper paths"
if ! ./ci/process-unity-package.sh; then
    print_error "Failed to create and process Unity package"
fi
print_success "Unity package created successfully"

# Step 10: Create a version tag locally
print_step "Creating local version tag $VERSION"
git tag "$VERSION"
print_success "Tag $VERSION created locally"
echo -e "${YELLOW}Note: Tag was only created locally. Use 'git push origin $VERSION' to push it to remote when ready.${NC}"

echo -e "\n${GREEN}Release process completed successfully!${NC}"
echo "Release artifacts are available in the builds directory:"
ls -la builds/
echo -e "\n${YELLOW}Note: All changes are local. You can review them before pushing to remote.${NC}" 