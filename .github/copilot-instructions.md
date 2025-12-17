# GitHub Copilot instructions (build/test)

Use the unified Cake script from the `build/` directory.

## Quick commands

- Restore tools: `dotnet tool restore`
- Build: `dotnet cake --script=build/build.cake --target=Build --solution=SRC/Apps/Apps.slnx -- <msbuild-args...>`
- Test: `dotnet cake --script=build/build.cake --target=Test -- <vstest-args...>`
- Build + Test (default): `dotnet cake --script=build/build.cake --solution=SRC/Apps/Apps.slnx -- <msbuild-args...> -- <vstest-args...>`
- Help: `dotnet cake --script=build/build.cake --target=Help`

## Options

- `--solution=<path>` (required for `Build` / `BuildAndTest`)
- `--msbuild-path=<path>` / `--vstest-path=<path>` (override tool discovery)
- `--test-src-path=<path>` (default: `TestSRC`)

## Separator rules (important)

- First standalone `--` starts passthrough arguments.
- Multiple options: space-separated (e.g., `/p:A=1 /p:B=2 /m:4`)
- Target-specific rules:
  - `Build`: Everything after first `--` → msbuild args
  - `Test`: Everything after first `--` → vstest args
  - `BuildAndTest`: Use second `--` to split:
    - between 1st and 2nd `--` → msbuild args
    - after 2nd `--` → vstest args

**Examples:**
```bash
# Build with multiple msbuild options
dotnet cake --script=build/build.cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release /p:Platform=x64 /m:4

# Test with multiple vstest options
dotnet cake --script=build/build.cake --target=Test -- /Parallel /Logger:trx /TestCaseFilter:"Priority=1"

# Build + Test with options for both
dotnet cake --script=build/build.cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release /m:4 -- /Parallel /Logger:trx
```

## Keep output small

- Prefer `dotnet cake --script=build/build.cake --target=Help` for usage.
- When debugging, filter logs: `dotnet cake --script=build/build.cake ... 2>&1 | Select-String -Pattern 'Resolved msbuild|Resolved vstest|Executing:|\[ERROR\]|\[WARNING\]'`
