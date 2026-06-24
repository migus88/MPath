# MPath release & build tasks.
#
# This wraps the flow that used to live in ci/release.sh and ci/publish-nuget.sh.
# The interactive y/n prompts are now discrete targets; choose explicitly which
# steps to run. The genuinely specialized work (version bumping via BSD sed,
# Unity batch-mode export) is still delegated to the scripts in ci/.
#
# macOS-oriented, like the underlying scripts: ci/version-increment.sh uses
# BSD `sed -i ""` and ci/process-unity-package.sh expects Unity Hub under
# /Applications/Unity/Hub/Editor.
#
# Typical release:
#   make bump PART=minor      # bump version in both files + commit
#   make release              # guards + build + test + artifacts + tag (and pushes the tag)
#   make publish-nuget        # publish to NuGet.org (key read from .env)
#
# Publishing secrets live in .env (gitignored; copy from .env.example). An
# exported NUGET_API_KEY / GITHUB_TOKEN in the environment overrides .env.

SLN          := src/mpath-source/Migs.MPath.sln
CORE_DIR     := src/mpath-source/Migs.MPath.Core
CSPROJ       := $(CORE_DIR)/Migs.MPath.Core.csproj
TESTS        := src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj
PACKAGE_JSON := src/mpath-unity-project/Packages/MPath/package.json
BUILDS_DIR   := builds

CONFIG       ?= Release
PART         ?= minor
# Recursively expanded: re-read after a `bump` so `release` sees the new version.
VERSION       = $(shell grep -o '"version": "[^"]*"' $(PACKAGE_JSON) | head -1 | cut -d'"' -f4)

MAKE_Q       := $(MAKE) --no-print-directory

.DEFAULT_GOAL := help
.PHONY: help version build test clean nuget unity package bump tag push release \
        publish publish-nuget publish-github check-branch check-clean check-tag

help: ## Show this help
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) \
	  | awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

version: ## Print the current version (from package.json)
	@echo $(VERSION)

build: ## Build the solution (CONFIG=Release by default)
	dotnet build $(SLN) -c $(CONFIG)

test: build ## Build then run the unit tests
	dotnet test $(TESTS) -c $(CONFIG) --no-build

clean: ## Remove build outputs and the builds/ directory
	dotnet clean $(SLN) -c $(CONFIG) || true
	rm -rf $(BUILDS_DIR)

bump: ## Bump version (PART=major|minor|patch, default minor) in both files + commit
	@new=$$(./ci/version-increment.sh $(PART) | tail -n1); \
	 git add $(CSPROJ) $(PACKAGE_JSON); \
	 git commit -m "version bump to $$new"; \
	 echo "bumped to $$new and committed"

nuget: build ## Build and copy the .nupkg into builds/
	@mkdir -p $(BUILDS_DIR)
	@pkg=$$(find $(CORE_DIR)/bin/$(CONFIG) -name 'Migs.MPath.*.nupkg' | sort -V | tail -n1); \
	 [ -n "$$pkg" ] || { echo "ERROR: no .nupkg found under $(CORE_DIR)/bin/$(CONFIG)"; exit 1; }; \
	 cp "$$pkg" $(BUILDS_DIR)/; \
	 echo "copied $$(basename $$pkg) -> $(BUILDS_DIR)/"

unity: ## Export + path-rewrite the .unitypackage into builds/ (needs Unity)
	./ci/process-unity-package.sh

package: nuget unity ## Produce both release artifacts (NuGet + Unity package)

tag: check-tag ## Create the local git tag for the current version
	git tag $(VERSION)
	@echo "created local tag $(VERSION) (push with: make push)"

push: ## Push the current version tag to origin
	git push origin $(VERSION)

release: ## Full release: guards, pull, build, test, artifacts, tag + push tag
	@$(MAKE_Q) check-branch
	@$(MAKE_Q) check-clean
	git pull
	@$(MAKE_Q) check-tag
	@$(MAKE_Q) build
	@$(MAKE_Q) test
	@$(MAKE_Q) nuget
	@$(MAKE_Q) unity
	@$(MAKE_Q) tag
	@$(MAKE_Q) push
	@echo ""
	@echo "Release $(VERSION) tagged and the tag pushed to origin. Artifacts:"
	@ls -la $(BUILDS_DIR)
	@echo ""
	@echo "Note: only the tag was pushed. If 'make bump' added a commit, run 'git push' to land it on master too."
	@echo "Next: 'make publish-nuget' / 'make publish-github'. Don't forget the README version badge + links."

publish: publish-nuget publish-github ## Publish the latest builds/*.nupkg to NuGet.org and GitHub Packages

publish-nuget: ## Push latest builds/*.nupkg to NuGet.org (NUGET_API_KEY from env or .env)
	@key="$${NUGET_API_KEY}"; \
	 if [ -z "$$key" ] && [ -f .env ]; then key=$$(grep -E '^NUGET_API_KEY=' .env | tail -n1 | cut -d= -f2- | tr -d '\r'); fi; \
	 [ -n "$$key" ] || { echo "ERROR: NUGET_API_KEY not set (add it to .env or export it)"; exit 1; }; \
	 pkg=$$(find $(BUILDS_DIR) -name 'Migs.MPath.*.nupkg' | sort -V | tail -n1); \
	 [ -n "$$pkg" ] || { echo "ERROR: no .nupkg in $(BUILDS_DIR)/ (run 'make nuget' first)"; exit 1; }; \
	 echo "publishing $$(basename $$pkg) to NuGet.org"; \
	 dotnet nuget push "$$pkg" --api-key "$$key" --source https://api.nuget.org/v3/index.json

publish-github: ## Push latest builds/*.nupkg to GitHub Packages (GITHUB_TOKEN from env or .env)
	@tok="$${GITHUB_TOKEN}"; \
	 if [ -z "$$tok" ] && [ -f .env ]; then tok=$$(grep -E '^GITHUB_TOKEN=' .env | tail -n1 | cut -d= -f2- | tr -d '\r'); fi; \
	 [ -n "$$tok" ] || { echo "ERROR: GITHUB_TOKEN not set (add it to .env or export it)"; exit 1; }; \
	 pkg=$$(find $(BUILDS_DIR) -name 'Migs.MPath.*.nupkg' | sort -V | tail -n1); \
	 [ -n "$$pkg" ] || { echo "ERROR: no .nupkg in $(BUILDS_DIR)/ (run 'make nuget' first)"; exit 1; }; \
	 dotnet nuget list source | grep -q github || \
	   dotnet nuget add source https://nuget.pkg.github.com/migus88/index.json --name github --username migus88 --password "$$tok"; \
	 echo "publishing $$(basename $$pkg) to GitHub Packages"; \
	 dotnet nuget push "$$pkg" --source github

check-branch: ## Fail if not on master (override with ALLOW_BRANCH=1)
	@b=$$(git rev-parse --abbrev-ref HEAD); \
	 if [ "$$b" != "master" ] && [ "$(ALLOW_BRANCH)" != "1" ]; then \
	   echo "ERROR: on branch '$$b', not master. Pass ALLOW_BRANCH=1 to override."; exit 1; \
	 fi; \
	 echo "branch: $$b"

check-clean: ## Fail if there are uncommitted changes
	@git diff --quiet && git diff --cached --quiet || \
	  { echo "ERROR: uncommitted changes present; commit or stash first."; exit 1; }

check-tag: ## Fail if a tag for the current version already exists
	@if git rev-parse "$(VERSION)" >/dev/null 2>&1; then \
	   echo "ERROR: tag $(VERSION) already exists"; exit 1; \
	 fi
