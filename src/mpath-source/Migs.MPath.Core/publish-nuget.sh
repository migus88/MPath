#!/bin/bash

# Script to build and publish the MPath NuGet package

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Build the project in Release mode
echo -e "${YELLOW}Building project in Release mode...${NC}"
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "Build failed. Exiting..."
    exit 1
fi

# Find the latest package
PACKAGE_PATH=$(find ./bin/Release -name "Migs.MPath.*.nupkg" | sort -V | tail -1)
PACKAGE_NAME=$(basename $PACKAGE_PATH)
echo -e "${GREEN}Package created: $PACKAGE_NAME${NC}"

# Step 2: Ask if user wants to publish to NuGet.org
read -p "Do you want to publish this package to NuGet.org? (y/n): " PUBLISH_NUGET

if [[ $PUBLISH_NUGET == "y" || $PUBLISH_NUGET == "Y" ]]; then
    read -p "Enter your NuGet API key: " API_KEY
    
    echo -e "${YELLOW}Publishing to NuGet.org...${NC}"
    dotnet nuget push $PACKAGE_PATH --api-key $API_KEY --source https://api.nuget.org/v3/index.json
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Package successfully published to NuGet.org!${NC}"
    else
        echo "Failed to publish to NuGet.org."
    fi
fi

# Step 3: Ask if user wants to publish to GitHub Packages
read -p "Do you want to publish this package to GitHub Packages? (y/n): " PUBLISH_GITHUB

if [[ $PUBLISH_GITHUB == "y" || $PUBLISH_GITHUB == "Y" ]]; then
    read -p "Enter your GitHub Personal Access Token: " GITHUB_TOKEN
    
    # Add GitHub source if it doesn't exist
    dotnet nuget list source | grep -q "github" || dotnet nuget add source https://nuget.pkg.github.com/migus88/index.json --name github --username migus88 --password $GITHUB_TOKEN
    
    echo -e "${YELLOW}Publishing to GitHub Packages...${NC}"
    dotnet nuget push $PACKAGE_PATH --source github
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Package successfully published to GitHub Packages!${NC}"
    else
        echo "Failed to publish to GitHub Packages."
    fi
fi

echo -e "${GREEN}All operations completed!${NC}" 