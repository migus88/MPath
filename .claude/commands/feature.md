---
description: Implement a feature end-to-end on a fresh branch and open a PR
argument-hint: <description of the feature to implement>
---

## Your task

Take the feature request below and carry it all the way from a clean branch to an open pull request. Work autonomously through every step; only stop to ask the user if the request is too ambiguous to implement safely or if a step fails in a way you cannot resolve.

<feature_request>
$ARGUMENTS
</feature_request>

If `$ARGUMENTS` is empty, ask the user what feature they want before doing anything else.

### 1. Sync the base branch

- Determine the repo's default branch (do not assume `main` — this repo uses `master`):
  ```bash
  BASE=$(git remote show origin | sed -n '/HEAD branch/s/.*: //p')
  ```
- Confirm the working tree is clean (`git status --porcelain`). If there are uncommitted changes, **stop and report them** — do not stash, reset, or discard without explicit confirmation.
- Switch to the base branch and fast-forward:
  ```bash
  git checkout "$BASE" && git pull --ff-only
  ```

### 2. Create a branch with an appropriate prefix

- Choose the prefix from the nature of the work: `feature/` for new functionality (default), `fix/` for bug fixes, `docs/` for docs-only changes, `refactor/`, `chore/` for tooling/CI.
- Derive a short kebab-case slug from the request (e.g. `feature/path-caching-ttl`).
- Create and switch to it: `git checkout -b <prefix><slug>`.

### 3. Implement the feature

- **Read `CLAUDE.md` first.** Most important: the library source lives **only** in `src/mpath-unity-project/Packages/MPath/Source/` — edit there, never in `src/mpath-source/` (which just links those files). Keep `Source/` free of `UnityEngine` references.
- Follow existing conventions: the `unsafe` pointer-based hot path, `[MethodImpl(AggressiveInlining)]` on per-cell/per-neighbor helpers, and threading any new public setting through `IPathfinderSettings` → `FastPathfinderSettings` → the relevant `Pathfinder` method.
- Add or update tests under `src/mpath-source/Migs.MPath.Tests/` for the new behavior.
- **Verify before proceeding** — do not open a PR on a red build:
  ```bash
  dotnet build src/mpath-source/Migs.MPath.sln
  dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj
  ```
  If anything fails, fix it; if you can't, stop and report.

### 4. Add / update documentation

- Update `docs/` to match any public API change: `docs/api/` mirrors public types (one file per type), `docs/guides/` covers usage.
- Update `README.md` if the public surface, quick-start, or feature list changes.
- Keep `CLAUDE.md` accurate if you change the architecture, the common commands, or the release flow.

### 5. Open the pull request

- Stage and commit with a clear, conventional message. End the commit message body with:
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  ```
- Push the branch: `git push -u origin <branch>`.
- Open the PR against the base branch:
  ```bash
  gh pr create --base "$BASE" --title "<concise title>" --body "<body>"
  ```
  The body should summarize **what** changed and **why**, list the verification you ran (build + tests), call out any docs updated, and end with:
  ```
  🤖 Generated with [Claude Code](https://claude.com/claude-code)
  ```
- Report the final PR URL back to the user.
