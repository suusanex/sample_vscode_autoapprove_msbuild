# Quick Start: Unified Build and Test Script

**Feature**: 001-unified-build-script  
**Version**: 1.0  
**Date**: December 16, 2025

## Overview

This guide helps you get started with the unified build and test execution script. In 5 minutes, you'll be running builds and tests with a single command.

---

## Prerequisites

### Required

- **.NET SDK 8.0 or later**
  - Check: `dotnet --version`
  - Install: [Download .NET SDK](https://dotnet.microsoft.com/download)

- **Visual Studio 2022 or Build Tools**
  - MSBuild and vstest.console.exe are included
  - Install: [Visual Studio](https://visualstudio.microsoft.com/) or [Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)

### Optional

- **Git** (for repository operations)
- **Windows 10 or later** (primary target platform)

---

## Installation

### Step 1: Install Cake.Tool

#### Option A: Global Installation (Recommended for Local Development)

```bash
dotnet tool install --global Cake.Tool
```

**Verify**:
```bash
dotnet cake --version
```

**Expected Output**: `Cake version X.Y.Z`

---

#### Option B: Local Installation (Recommended for CI/Teams)

**Create tool manifest** (if not exists):
```bash
dotnet new tool-manifest
```

**Install Cake locally**:
```bash
dotnet tool install Cake.Tool
```

**Restore tools** (for team members):
```bash
dotnet tool restore
```

**Verify**:
```bash
dotnet cake --version
```

---

### Step 2: Verify Build Script

**Check that build script exists**:
```bash
# From repository root
dir build\build.cake
```

**Expected**: File exists at `build\build.cake`

**If missing**: Build script will be created during implementation phase.

---

## Basic Usage

### Build Only

**Command**:
```bash
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx
```

**What it does**:
- Locates msbuild automatically
- Builds the specified solution
- Returns exit code (0=success, non-zero=failure)

**Output**:
```text
[INFO] Resolved msbuild: C:\Program Files\...\MSBuild.exe
[INFO] Executing: msbuild "SRC\Apps\Apps.slnx"
<msbuild build output>
Build succeeded.
```

---

### Test Only

**Command**:
```bash
dotnet cake --target=Test
```

**What it does**:
- Discovers test DLLs in `TestSRC` directory
- Locates vstest.console.exe automatically
- Runs all discovered tests
- Returns exit code (0=success, non-zero=failure)

**Output**:
```text
[INFO] Discovering test DLLs in: D:\Repos\App\TestSRC
[INFO] Found 2 test assemblies:
  - TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll
  - TestSRC\FeatureTest\bin\Debug\net10.0-windows\win-x64\Feature_Test.dll
[INFO] Executing: vstest.console.exe "..." "..."
<vstest execution output>
Test Run Successful.
```

---

### Build + Test (Default)

**Command**:
```bash
dotnet cake --solution=SRC/Apps/Apps.slnx
```

**What it does**:
- Builds the solution first
- If build succeeds, runs tests
- If build fails, stops immediately
- Returns last tool's exit code

**Shorthand**: Omitting `--target` defaults to `BuildAndTest`

---

## Common Scenarios

### Scenario 1: Debug Build + Run All Tests

```bash
dotnet cake --solution=SRC/Apps/Apps.slnx
```

**Use Case**: Standard local development workflow

---

### Scenario 2: Release Build + Run Specific Tests

```bash
dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /TestCaseFilter:"Priority=1"
```

**Explanation**:
- `-- /p:Configuration=Release` → passed to msbuild
- `-- /TestCaseFilter:"Priority=1"` → passed to vstest

**Use Case**: Pre-deployment smoke tests

---

### Scenario 3: Build Only (Skip Tests)

```bash
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release
```

**Use Case**: Quick iteration when tests aren't needed

---

### Scenario 4: Test Only (After Manual Build)

```bash
dotnet cake --target=Test
```

**Use Case**: Re-run tests after making code changes in IDE

---

### Scenario 5: Parallel Build + Parallel Tests

```bash
dotnet cake --solution=SRC/Apps/Apps.slnx -- /m:4 -- /Parallel
```

**Explanation**:
- `/m:4` → msbuild uses 4 cores
- `/Parallel` → vstest runs tests in parallel

**Use Case**: Faster execution on multi-core machines

---

### Scenario 6: Verbose Logging

```bash
dotnet cake --solution=SRC/Apps/Apps.slnx -- /v:detailed -- /Logger:"console;verbosity=detailed"
```

**Explanation**:
- `/v:detailed` → msbuild verbose output
- `/Logger:...` → vstest verbose output

**Use Case**: Debugging build or test failures

---

## Advanced Usage

### Override Tool Paths

**Scenario**: Using non-standard Visual Studio installation

```bash
# Via environment variable
set MSBUILD_PATH=C:\CustomVS\MSBuild\MSBuild.exe
set VSTEST_PATH=C:\CustomVS\vstest.console.exe
dotnet cake --solution=SRC/Apps/Apps.slnx

# Via CLI argument
dotnet cake --solution=SRC/Apps/Apps.slnx --msbuild-path=C:\CustomVS\MSBuild\MSBuild.exe --vstest-path=C:\CustomVS\vstest.console.exe
```

---

### Custom Test Source Directory

**Scenario**: Tests are in non-standard location

```bash
dotnet cake --target=Test --test-src-path=CustomTests
```

---

### Generate Test Report

```bash
dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Logger:trx /ResultsDirectory:TestResults
```

**Output**: `TestResults/*.trx` files (VSTest format)

**View Results**: Open `.trx` files in Visual Studio or convert to HTML

---

## CI Integration

### Azure Pipelines

**File**: `azure-pipelines.yml`

```yaml
trigger:
- main

pool:
  vmImage: 'windows-latest'

steps:
- task: UseDotNet@2
  displayName: 'Install .NET SDK'
  inputs:
    packageType: 'sdk'
    version: '8.x'

- task: PowerShell@2
  displayName: 'Restore Tools'
  inputs:
    script: 'dotnet tool restore'

- task: PowerShell@2
  displayName: 'Build and Test'
  inputs:
    script: 'dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Logger:trx /ResultsDirectory:$(Agent.TempDirectory)'
    failOnStderr: false

- task: PublishTestResults@2
  displayName: 'Publish Test Results'
  condition: succeededOrFailed()
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '$(Agent.TempDirectory)/**/*.trx'
```

---

### GitHub Actions

**File**: `.github/workflows/build-and-test.yml`

```yaml
name: Build and Test

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.x'

    - name: Restore Tools
      run: dotnet tool restore

    - name: Build and Test
      run: dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel

    - name: Upload Test Results
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: test-results
        path: TestResults/**/*.trx
```

---

## Troubleshooting

### Issue: "Cake not found"

**Error**:
```text
Could not execute because the specified command or file was not found.
```

**Solution**: Install Cake.Tool
```bash
dotnet tool install --global Cake.Tool
# or
dotnet tool restore
```

---

### Issue: "msbuild not found"

**Error**:
```text
[ERROR] msbuild not found. Install Visual Studio Build Tools or set MSBUILD_PATH.
```

**Solution**:
1. Install Visual Studio Build Tools
2. Or set `MSBUILD_PATH` environment variable:
   ```bash
   set MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
   ```

---

### Issue: "vstest.console.exe not found"

**Error**:
```text
[ERROR] vstest.console.exe not found. Install Visual Studio Test Platform or set VSTEST_PATH.
```

**Solution**:
1. Install Visual Studio (ensure "Testing tools" workload is installed)
2. Or set `VSTEST_PATH` environment variable:
   ```bash
   set VSTEST_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe
   ```

---

### Issue: "No test assemblies found"

**Warning**:
```text
[WARNING] No test assemblies found matching discovery criteria. Skipping test execution.
```

**Possible Causes**:
1. Test projects not built yet → Run `dotnet cake --target=Build --solution=...` first
2. Test DLLs don't match naming convention → Rename to `*_Test.dll`
3. Wrong output path → Ensure tests output to `bin\Debug\net10.0-windows*\win-x64\`

**Verify**:
```bash
dir /s /b TestSRC\*_Test.dll
```

---

### Issue: "Solution file not found"

**Error**:
```text
[ERROR] Solution file not found: SRC/Apps/Apps.slnx
```

**Solution**:
- Check path is correct (use `dir` to verify)
- Use absolute path if needed:
  ```bash
  dotnet cake --target=Build --solution=D:\Repos\MyApp\SRC\Apps\Apps.slnx
  ```

---

### Issue: "Build failed"

**Exit Code**: Non-zero

**Debugging**:
1. Run with verbose logging:
   ```bash
   dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /v:detailed
   ```
2. Check msbuild output for specific errors
3. Try building directly in Visual Studio to isolate issue

---

### Issue: "Tests failed"

**Exit Code**: Non-zero

**Debugging**:
1. Run with detailed test output:
   ```bash
   dotnet cake --target=Test -- /Logger:"console;verbosity=detailed"
   ```
2. Review test failure messages
3. Run specific test in Visual Studio Test Explorer

---

## Tips & Tricks

### Tip 1: Use Aliases

**Create PowerShell alias** (add to `$PROFILE`):
```powershell
function Build-Solution { dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx }
function Test-All { dotnet cake --target=Test }
function Build-And-Test { dotnet cake --solution=SRC/Apps/Apps.slnx }

Set-Alias build Build-Solution
Set-Alias test Test-All
Set-Alias bt Build-And-Test
```

**Usage**:
```bash
build       # Quick build
test        # Quick test
bt          # Build and test
```

---

### Tip 2: Create Batch Files

**File**: `build.cmd`
```batch
@echo off
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx %*
```

**File**: `test.cmd`
```batch
@echo off
dotnet cake --target=Test %*
```

**Usage**:
```bash
build /p:Configuration=Release
test /TestCaseFilter:"Category=Smoke"
```

---

### Tip 3: Use Environment Variables for Stability

**Set once** (in system environment):
```bash
setx MSBUILD_PATH "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
setx VSTEST_PATH "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
```

**Benefit**: Script always uses same tools (no vswhere dependency)

---

### Tip 4: GitHub Copilot Terminal Approval

**Configure auto-approval** (VS Code settings.json):
```json
{
  "github.copilot.terminalAutomation.approvedCommands": [
    "dotnet cake"
  ]
}
```

**Benefit**: Copilot can run builds/tests without manual approval

---

## Next Steps

### Learn More

- **Architecture**: See [plan.md](plan.md) for design decisions
- **CLI Reference**: See [contracts/cli-interface.md](contracts/cli-interface.md) for full syntax
- **Discovery Spec**: See [contracts/discovery-spec.md](contracts/discovery-spec.md) for test DLL matching rules

### Customize

- **Add Custom Targets**: Edit `build/build.cake` to add `Clean`, `Restore`, etc.
- **Modify Discovery**: Change regex patterns in `build/build.cake` for different naming conventions
- **Extend Logging**: Add custom log formatters or file logging

---

## Support

### Getting Help

- **Check Logs**: Script logs show tool paths and commands executed
- **Manual Testing**: Copy command from `[INFO] Executing: ...` and run manually
- **Verify Tools**: Check msbuild/vstest work independently before using script

---

**Version**: 1.0  
**Status**: Ready to use  
**Feedback**: Report issues via GitHub Issues or team channels
