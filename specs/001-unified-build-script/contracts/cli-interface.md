# CLI Interface Contract

**Feature**: 001-unified-build-script  
**Version**: 1.0  
**Date**: December 16, 2025

## Overview

This document defines the command-line interface contract for the unified build and test execution script. This contract is stable and forms the basis for CI configurations and GitHub Copilot terminal approval rules.

---

## Entry Point

**Command**: `dotnet cake`

**Script Location**: `build/build.cake` (relative to repository root)

**Prerequisites**:
- .NET SDK 8.0 or later installed
- Cake.Tool installed (global or local via `dotnet tool restore`)

---

## Target Specification

### Syntax

```text
--target=<TargetName>
```

### Valid Targets

| Target | Description | Dependencies | Default Behavior |
|--------|-------------|--------------|------------------|
| `Build` | Execute msbuild only | Requires `--solution` | Builds specified solution |
| `Test` | Execute vstest only | None | Discovers and runs all eligible tests |
| `BuildAndTest` | Execute Build then Test | Requires `--solution` | Build + Test in sequence |
| `Default` | Alias for BuildAndTest | Requires `--solution` | Same as BuildAndTest |

### Examples

```bash
# Explicit target
dotnet cake --target=Build --solution=Apps.slnx

# Default target (BuildAndTest)
dotnet cake --solution=Apps.slnx

# Test only
dotnet cake --target=Test
```

---

## Required Arguments

### --solution (Conditional)

**Required For**: `Build`, `BuildAndTest`, `Default`  
**Optional For**: `Test`

**Format**: `--solution=<path>`

**Path Handling**:
- Relative paths resolved from current working directory
- Absolute paths used as-is
- Must be valid `.sln`, `.slnx`, or `.csproj` file

**Examples**:
```bash
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx
dotnet cake --target=Build --solution="D:\Repos\MyApp\My Solution.sln"
dotnet cake --target=Build --solution=../OtherRepo/App.csproj
```

**Validation**:
- ERROR if file doesn't exist
- ERROR if `--target=Build` and `--solution` not provided

---

## Optional Arguments

### --msbuild-path

**Purpose**: Override msbuild.exe location

**Format**: `--msbuild-path=<path>`

**Behavior**:
- If specified and exists → use this path (skip auto-discovery)
- If specified and doesn't exist → ERROR (fail fast)
- If not specified → auto-discover via vswhere/PATH

**Example**:
```bash
dotnet cake --target=Build --solution=App.slnx --msbuild-path="C:\BuildTools\MSBuild\MSBuild.exe"
```

**Alternative**: Set `MSBUILD_PATH` environment variable (same behavior)

---

### --vstest-path

**Purpose**: Override vstest.console.exe location

**Format**: `--vstest-path=<path>`

**Behavior**:
- If specified and exists → use this path (skip auto-discovery)
- If specified and doesn't exist → ERROR (fail fast)
- If not specified → auto-discover via vswhere/common paths/PATH

**Example**:
```bash
dotnet cake --target=Test --vstest-path="C:\VSTest\vstest.console.exe"
```

**Alternative**: Set `VSTEST_PATH` environment variable (same behavior)

---

### --test-src-path

**Purpose**: Override TestSRC root directory

**Format**: `--test-src-path=<path>`

**Default**: `TestSRC` (relative to repository root)

**Behavior**:
- Relative paths resolved from repository root (`.git` parent directory)
- Absolute paths used as-is

**Example**:
```bash
dotnet cake --target=Test --test-src-path=tests
dotnet cake --target=Test --test-src-path="D:\CustomTestLocation"
```

---

## Passthrough Arguments

### MSBuild Arguments

**Separator**: First `--` after Cake arguments

**Format**:
```text
dotnet cake [Cake args] -- [msbuild args]
```

**Behavior**:
- All arguments after first `--` collected as array
- Passed to msbuild **in exact order, with no modifications**
- No validation or interpretation by script

**Examples**:
```bash
# Set Configuration property
dotnet cake --target=Build --solution=App.slnx -- /p:Configuration=Release

# Enable parallel build
dotnet cake --target=Build --solution=App.slnx -- /m:4

# Multiple properties
dotnet cake --target=Build --solution=App.slnx -- /p:Configuration=Release /p:Platform=x64 /m:4

# Verbosity
dotnet cake --target=Build --solution=App.slnx -- /v:detailed
```

**Quoting**: Arguments with spaces must be quoted (standard shell rules):
```bash
dotnet cake --target=Build --solution=App.slnx -- "/p:DefineConstants=MY_CONSTANT;ANOTHER"
```

---

### VSTest Arguments

**Separator**: Second `--` (or first `--` if `Test` target)

**Format**:
```text
# BuildAndTest target (two separators)
dotnet cake --target=BuildAndTest --solution=App.slnx -- [msbuild args] -- [vstest args]

# Test target (one separator)
dotnet cake --target=Test -- [vstest args]
```

**Behavior**:
- All arguments after second `--` (or first `--` for Test target) collected as array
- Passed to vstest.console.exe **in exact order, with no modifications**
- No validation or interpretation by script

**Examples**:
```bash
# Test filter
dotnet cake --target=Test -- /TestCaseFilter:"Priority=1"

# Parallel execution
dotnet cake --target=Test -- /Parallel

# Custom logger
dotnet cake --target=Test -- /Logger:trx /Logger:"console;verbosity=detailed"

# BuildAndTest with both tool args
dotnet cake --target=BuildAndTest --solution=App.slnx -- /p:Configuration=Release -- /Parallel /Logger:trx
```

**Quoting**: Arguments with colons/spaces must be quoted:
```bash
dotnet cake --target=Test -- "/TestCaseFilter:Category=Smoke|Category=Integration"
```

---

## Exit Codes

### Success

**Exit Code**: `0`

**Scenarios**:
- Build succeeded (msbuild returned 0)
- Tests passed (vstest returned 0)
- BuildAndTest: Build succeeded AND tests passed
- Test target with no DLLs discovered (WARNING logged, but not error)

---

### Failure

**Exit Code**: Non-zero (matches tool exit code)

**Scenarios**:
- Build failed → return msbuild exit code
- Tests failed → return vstest exit code
- BuildAndTest + build failed → return msbuild exit code (tests not executed)
- BuildAndTest + build succeeded but tests failed → return vstest exit code

---

### Script Errors

**Exit Code**: `1` (generic error)

**Scenarios**:
- Required tool (msbuild/vstest) not found
- Required argument missing (e.g., `--solution` for Build target)
- Specified file/directory doesn't exist
- Invalid argument format

**Error Output**: Sent to stderr with `[ERROR]` prefix

---

## Environment Variables

### MSBUILD_PATH

**Type**: String (file path)  
**Purpose**: Override msbuild.exe location  
**Priority**: Same as `--msbuild-path` (CLI arg takes precedence)

**Example**:
```bash
export MSBUILD_PATH="/opt/msbuild/msbuild"
dotnet cake --target=Build --solution=App.slnx
```

---

### VSTEST_PATH

**Type**: String (file path)  
**Purpose**: Override vstest.console.exe location  
**Priority**: Same as `--vstest-path` (CLI arg takes precedence)

**Example**:
```bash
set VSTEST_PATH=C:\CustomVSTest\vstest.console.exe
dotnet cake --target=Test
```

---

## Output Format

### Standard Output (stdout)

**Content**:
- `[INFO]` log messages from script
- All stdout from msbuild (build output)
- All stdout from vstest (test results)

**Characteristics**:
- Real-time streaming (no buffering)
- Colorized output preserved (if terminal supports it)

---

### Standard Error (stderr)

**Content**:
- `[ERROR]` log messages from script
- All stderr from msbuild (warnings/errors)
- All stderr from vstest (test framework errors)

**Characteristics**:
- Real-time streaming (no buffering)
- Used for failure detection in CI (non-empty stderr may indicate issues)

---

### Logging Format

**Script Logs**:
```text
[INFO] <timestamp> | <message>
[WARNING] <timestamp> | <message>
[ERROR] <timestamp> | <message>
```

**Tool Output**: Unmodified (passed through directly)

---

## Command Echoing

### Purpose

Enable manual reproduction of commands for debugging.

### Format

Before executing a tool, the script logs the exact command line:

```text
[INFO] Executing: msbuild "SRC\Apps\Apps.slnx" /p:Configuration=Release /m:4
[INFO] Executing: vstest.console.exe "TestSRC\MainAppTest\bin\...\MainAppTest_Test.dll" "TestSRC\Other_Test\bin\...\Other_Test.dll" /Parallel
```

**Characteristics**:
- Absolute paths resolved and shown
- Quoting preserved
- Can be copy-pasted into terminal for manual execution

---

## Compatibility Guarantees

### Stable (Will Not Change)

- Target names (`Build`, `Test`, `BuildAndTest`, `Default`)
- Argument names (`--solution`, `--msbuild-path`, `--vstest-path`, `--test-src-path`)
- Separator syntax (`--` for passthrough arguments)
- Exit code semantics (0=success, non-zero=failure)
- Environment variable names (`MSBUILD_PATH`, `VSTEST_PATH`)

### May Change (Non-Breaking)

- Log message text (not part of contract)
- Tool discovery heuristics (internal implementation)
- Performance optimizations (execution order, caching)

### Breaking Changes Require Version Bump

- Removing or renaming arguments
- Changing exit code semantics
- Changing separator syntax
- Changing target names

---

## Usage Patterns for CI

### Azure Pipelines

```yaml
steps:
- task: UseDotNet@2
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
    script: 'dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Logger:trx'
    failOnStderr: false
  
- task: PublishTestResults@2
  condition: succeededOrFailed()
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '**/*.trx'
```

---

### GitHub Actions

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v3
  with:
    dotnet-version: '8.x'

- name: Restore Tools
  run: dotnet tool restore

- name: Build and Test
  run: dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel
```

---

### Local Development

```bash
# First time setup
dotnet tool install --global Cake.Tool

# Quick build and test
dotnet cake --solution=SRC/Apps/Apps.slnx

# Build only (faster for iteration)
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Debug

# Test only (after manual build in IDE)
dotnet cake --target=Test

# Advanced: Custom logging and filters
dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release /v:minimal -- /TestCaseFilter:"FullyQualifiedName~MainApp" /Logger:"console;verbosity=detailed"
```

---

## Error Messages Reference

### ERR-001: Tool Not Found

```text
[ERROR] msbuild not found. Install Visual Studio Build Tools or set MSBUILD_PATH environment variable.
Attempted paths:
  - vswhere query: Microsoft.Component.MSBuild
  - PATH environment variable
```

**Resolution**: Install VS Build Tools or set `MSBUILD_PATH`

---

### ERR-002: Missing Required Argument

```text
[ERROR] Build target requires --solution argument.
Usage: dotnet cake --target=Build --solution=<path>
```

**Resolution**: Provide `--solution` argument

---

### ERR-003: File Not Found

```text
[ERROR] Solution file not found: D:\Repos\App\NotExist.sln
```

**Resolution**: Verify path is correct and file exists

---

### ERR-004: Directory Not Found

```text
[ERROR] TestSRC directory not found at: D:\Repos\App\TestSRC
```

**Resolution**: Verify TestSRC directory exists or use `--test-src-path` to specify alternate location

---

### WRN-001: No Tests Discovered

```text
[WARNING] No test assemblies found matching discovery criteria. Skipping test execution.
Search criteria:
  - Root: D:\Repos\App\TestSRC
  - Pattern: *_Test.dll in /bin/Debug/net10.0-windows.*/win-x64/
  - Excluded: CppCliModuleTest.dll
```

**Resolution**: Verify test projects are built and match naming/path conventions

---

## Copilot Integration

### Terminal Approval Strategy

**Stable Command**: `dotnet cake`

**Approval Rule Configuration**:
```json
{
  "terminalAutomation": {
    "approvedCommands": [
      {
        "pattern": "dotnet cake",
        "description": "Unified build and test script"
      }
    ]
  }
}
```

**Benefits**:
- Single approval for all build/test variations
- Arguments after `--` don't require new approvals
- Copilot can dynamically adjust msbuild/vstest options without user intervention

---

**Version**: 1.0  
**Status**: Contract complete and stable
