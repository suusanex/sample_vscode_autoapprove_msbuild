# Build Script Quick Reference

This repo uses Cake as a single entry point for build and test execution.

## Prerequisites

- .NET SDK 8.0+
- Visual Studio 2022 (or Build Tools) for `msbuild` / `vstest.console.exe`

## Setup

```powershell
# From repo root
dotnet tool restore
```

## Commands

```powershell
# Show usage
dotnet cake --target=Help

# Build only
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release

# Test only
dotnet cake --target=Test -- /Parallel /Logger:trx

# Build and test (default target)
dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel
```

## Overrides

```powershell
# Override tool paths
$env:MSBUILD_PATH = "C:\\Path\\To\\MSBuild.exe"
$env:VSTEST_PATH = "C:\\Path\\To\\vstest.console.exe"

# Override TestSRC root
dotnet cake --target=Test --test-src-path=TestSRC -- /Logger:trx
```
