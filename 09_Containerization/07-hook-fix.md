# Pre-push Hook — Performance Fix

## File changed

`.githooks/pre-push`

## Problem

The original hook called `dotnet format --verify-no-changes --include <file>` once per changed `.cs` file. Each call boots a new Roslyn compiler process, which takes 2–3 seconds of startup overhead regardless of how much code is actually checked. With 10 changed files across one solution, that is 10 separate Roslyn startups before the build even begins.

On a first push (when the remote branch does not exist yet), the hook compares against the merge-base with `main`, so every `.cs` file changed since branching is included. That can easily be 30–50 files, making the hook take several minutes.

## Fix

Collect all changed files per solution into a list during the diff loop, then call `dotnet format` **once per solution** with all `--include` flags in a single invocation.

**Before (one call per file):**
```sh
# called N times, one per changed file
dotnet format "$solution" --verify-no-changes --include "$file"
```

**After (one call per solution):**
```sh
# include_args = "--include file1 --include file2 --include file3 ..."
dotnet format "$solution" --verify-no-changes $include_args
```

## Result

| Scenario | Before | After |
|---|---|---|
| 10 files changed in one solution | ~10 Roslyn startups | 1 Roslyn startup |
| 3 solutions touched | ~30 Roslyn startups | 3 Roslyn startups |
| `dotnet build` per solution | unchanged | unchanged |

Format checking correctness is identical — the same files are passed to `dotnet format`, just in a single call instead of N calls.

## Additional note

Once a branch is published to the remote, subsequent pushes only check commits added since the last push (not the full branch history). This alone reduces the file count significantly on incremental pushes. The batch fix helps most on first push or after large commits.
