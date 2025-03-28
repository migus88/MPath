#!/bin/bash

# Script to build and publish the MPath NuGet package

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if builds directory exists
BUILDS_DIR="builds"
if [ ! -d "$BUILDS_DIR" ]; then
    echo -e "${RED}Error: 'builds' directory not found.${NC}"
    echo "Make sure you run the release script first to generate the NuGet package."
    exit 1
fi

# Find the latest package in builds directory
PACKAGE_PATH=$(find ./$BUILDS_DIR -name "Migs.MPath.*.nupkg" | sort -V | tail -1)

if [ -z "$PACKAGE_PATH" ]; then
    echo -e "${RED}Error: No NuGet package found in 'builds' directory.${NC}"
    echo "Make sure you run the release script first to generate the NuGet package."
    exit 1
fi

PACKAGE_NAME=$(basename $PACKAGE_PATH)
echo -e "${GREEN}Found package: $PACKAGE_NAME${NC}"

# Step 2: Ask if user wants to publish to NuGet.org
read -p "Do you want to publish this package to NuGet.org? (y/n): " PUBLISH_NUGET

if [[ $PUBLISH_NUGET == "y" || $PUBLISH_NUGET == "Y" ]]; then
    # Check if API key is in environment variable
    API_KEY=${NUGET_API_KEY:-""}
    
    # If API key is not set in environment, prompt for it
    if [ -z "$API_KEY" ]; then
        echo -e "${YELLOW}NUGET_API_KEY environment variable not found.${NC}"
        read -p "Enter your NuGet API key: " API_KEY
    else
        echo -e "${GREEN}Using NuGet API key from environment variable.${NC}"
    fi
    
    echo -e "${YELLOW}Publishing to NuGet.org...${NC}"
    dotnet nuget push $PACKAGE_PATH --api-key $API_KEY --source https://api.nuget.org/v3/index.json
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Package successfully published to NuGet.org!${NC}"
    else
        echo -e "${RED}Failed to publish to NuGet.org.${NC}"
    fi
fi

# Step 3: Ask if user wants to publish to GitHub Packages
read -p "Do you want to publish this package to GitHub Packages? (y/n): " PUBLISH_GITHUB

if [[ $PUBLISH_GITHUB == "y" || $PUBLISH_GITHUB == "Y" ]]; then
    # Check if GitHub token is in environment variable
    GITHUB_PAT=${GITHUB_TOKEN:-""}
    
    # If GitHub token is not set in environment, prompt for it
    if [ -z "$GITHUB_PAT" ]; then
        echo -e "${YELLOW}GITHUB_TOKEN environment variable not found.${NC}"
        read -p "Enter your GitHub Personal Access Token: " GITHUB_PAT
    else
        echo -e "${GREEN}Using GitHub token from environment variable.${NC}"
    fi
    
    # Add GitHub source if it doesn't exist
    dotnet nuget list source | grep -q "github" || dotnet nuget add source https://nuget.pkg.github.com/migus88/index.json --name github --username migus88 --password $GITHUB_PAT
    
    echo -e "${YELLOW}Publishing to GitHub Packages...${NC}"
    dotnet nuget push $PACKAGE_PATH --source github
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Package successfully published to GitHub Packages!${NC}"
    else
        echo -e "${RED}Failed to publish to GitHub Packages.${NC}"
    fi
fi

echo -e "${GREEN}All operations completed!${NC}" 