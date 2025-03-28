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

# Function to ask if user wants to execute or skip a step
ask_execute_step() {
    local step_name="$1"
    read -p "Execute step: $step_name? (y/n): " choice
    if [[ "$choice" =~ ^[Yy]$ ]]; then
        return 0  # Execute step
    else
        echo "Skipping step: $step_name"
        return 1  # Skip step
    fi
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
if ask_execute_step "Check current branch"; then
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
fi

# Step 2: Check for unstaged or uncommitted changes
if ask_execute_step "Check for unstaged or uncommitted changes"; then
    print_step "Checking for unstaged or uncommitted changes"
    if ! git diff --quiet || ! git diff --cached --quiet; then
        print_error "There are unstaged or uncommitted changes. Please commit or stash them first."
    fi
    print_success "No unstaged or uncommitted changes found"
fi

# Step 3: Pull the latest code from remote
if ask_execute_step "Pull the latest code from remote"; then
    print_step "Pulling latest code from remote"
    if ! git pull; then
        print_error "Failed to pull latest code from remote"
    fi
    print_success "Successfully pulled latest code"
fi

# Step 4: Ask if version increment is needed
if ask_execute_step "Version increment"; then
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
fi

# Step 5: Check if git tag already exists
if ask_execute_step "Check if git tag already exists"; then
    print_step "Checking if git tag v$VERSION already exists"
    if git rev-parse "v$VERSION" >/dev/null 2>&1; then
        print_error "Tag v$VERSION already exists"
    fi
    print_success "Tag v$VERSION does not exist yet"
fi

# Step 6: Build the solution
if ask_execute_step "Build the solution"; then
    print_step "Building solution"
    if ! dotnet build src/mpath-source/Migs.MPath.sln -c Release; then
        print_error "Build failed"
    fi
    print_success "Build completed successfully"
fi

# Step 7: Run unit tests
if ask_execute_step "Run unit tests"; then
    print_step "Running unit tests"
    if ! dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj -c Release --no-build; then
        print_error "Tests failed"
    fi
    print_success "All tests passed"
fi

# Step 8: Copy NuGet package to builds directory
if ask_execute_step "Copy NuGet package to builds directory"; then
    print_step "Copying NuGet package to builds directory"
    mkdir -p builds

    # Find the NuGet package
    NUGET_PACKAGE=$(find src/mpath-source/Migs.MPath.Core/bin/Release -name "Migs.MPath.*.nupkg" | sort -V | tail -n1)
    if [ -z "$NUGET_PACKAGE" ]; then
        print_error "NuGet package not found"
    fi

    cp "$NUGET_PACKAGE" builds/
    print_success "NuGet package copied to builds directory"
fi

# Step 9: Create package tarball with modified paths
if ask_execute_step "Create Unity package with proper paths"; then
    print_step "Creating Unity package with proper paths"
    if ! ./ci/process-unity-package.sh; then
        print_error "Failed to create and process Unity package"
    fi
    print_success "Unity package created successfully"
fi

# Step 10: Create a version tag locally
if ask_execute_step "Create local version tag"; then
    print_step "Creating local version tag v$VERSION"
    git tag "v$VERSION"
    print_success "Tag v$VERSION created locally"
    echo -e "${YELLOW}Note: Tag was only created locally. Use 'git push origin v$VERSION' to push it to remote when ready.${NC}"
fi

echo -e "\n${GREEN}Release process completed successfully!${NC}"
echo "Release artifacts are available in the builds directory:"
ls -la builds/
echo -e "\n${YELLOW}Note: All changes are local. You can review them before pushing to remote.${NC}" 