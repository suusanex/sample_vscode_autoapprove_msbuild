# Data Model: Unified Build and Test Execution Script

**Date**: December 16, 2025  
**Feature**: 001-unified-build-script

## Overview

This document defines the data structures, schemas, and state models for the unified build script. Since this is a build automation script (not a traditional application), the "data model" focuses on CLI arguments, file discovery criteria, and execution state.

---

## 1. CLI Argument Schema

### 1.1 Cake Target Specification

**Entity**: `TargetArgument`

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| target | string (enum) | No | "Default" | Target to execute: Build, Test, BuildAndTest, Default |

**Valid Values**:
- `Build`: Execute msbuild only
- `Test`: Execute vstest.console.exe only
- `BuildAndTest`: Execute Build then Test
- `Default`: Alias for BuildAndTest

**Usage Example**:
```bash
dotnet cake --target=Build
dotnet cake --target=Test
dotnet cake --target=BuildAndTest
dotnet cake  # Executes Default (BuildAndTest)
```

---

### 1.2 Solution/Project Path

**Entity**: `SolutionArgument`

| Field | Type | Required | Default | Validation |
|-------|------|----------|---------|-----------|
| solution | string (path) | Conditional* | null | Must be valid file path |

**Required When**: `target` is `Build` or `BuildAndTest`  
**Optional When**: `target` is `Test` (not used)

**Path Resolution**:
- Relative paths resolved from current working directory
- Absolute paths used as-is
- Must point to existing `.sln`, `.slnx`, or `.csproj` file

**Usage Example**:
```bash
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx
dotnet cake --target=Build --solution=D:\Repos\MyApp\MyApp.sln
```

---

### 1.3 Tool Path Overrides

**Entity**: `ToolPathArguments`

| Field | Type | Required | Source | Priority |
|-------|------|----------|--------|----------|
| msbuild-path | string (path) | No | CLI argument | 1 (highest) |
| MSBUILD_PATH | string (path) | No | Environment variable | 1 (highest) |
| vstest-path | string (path) | No | CLI argument | 1 (highest) |
| VSTEST_PATH | string (path) | No | Environment variable | 1 (highest) |

**Behavior**:
- If specified and file exists → use it
- If specified and file doesn't exist → ERROR (fail fast)
- If not specified → proceed with auto-discovery

**Usage Example**:
```bash
dotnet cake --target=Build --msbuild-path="C:\Tools\MSBuild\MSBuild.exe"
export MSBUILD_PATH="/usr/local/bin/msbuild"
dotnet cake --target=Test --vstest-path="C:\VSTest\vstest.console.exe"
```

---

### 1.4 Test Source Root Override

**Entity**: `TestSourcePathArgument`

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| test-src-path | string (path) | No | "TestSRC" | Root directory for test DLL discovery |

**Path Resolution**:
- Relative paths resolved from repository root (`.git` parent)
- Absolute paths used as-is

**Usage Example**:
```bash
dotnet cake --target=Test --test-src-path=tests
dotnet cake --target=Test --test-src-path=D:\Repos\MyApp\TestProjects
```

---

### 1.5 Passthrough Arguments

**Entity**: `PassthroughArguments`

| Field | Type | Required | Separator | Target Tool |
|-------|------|----------|-----------|-------------|
| msbuildArgs | string[] | No | First `--` | msbuild.exe |
| vstestArgs | string[] | No | Second `--` | vstest.console.exe |

**Parsing Rules**:
```text
dotnet cake [Cake args] -- [msbuild args] -- [vstest args]
            └─ Before ─┘    └─ Between ─┘    └─ After ─┘
                           first & second    second --
```

**Examples**:
```bash
# Build only: msbuild args after first --
dotnet cake --target=Build --solution=App.slnx -- /p:Configuration=Release /m:4

# Test only: vstest args after first -- (no second -- needed)
dotnet cake --target=Test -- /TestCaseFilter:"Priority=1" /Parallel

# BuildAndTest: msbuild args between --, vstest args after second --
dotnet cake --target=BuildAndTest --solution=App.slnx -- /p:Configuration=Release -- /Parallel
```

**Order Preservation**: Arguments passed to tools in the exact order received.

---

## 2. Tool Discovery Schema

### 2.1 Tool Discovery Result

**Entity**: `ToolDiscoveryResult`

| Field | Type | Description |
|-------|------|-------------|
| toolPath | string | Absolute path to tool executable |
| discoveryMethod | enum | How the tool was found |
| attemptedPaths | string[] | All paths tried during discovery (for error logging) |
| success | bool | Whether discovery succeeded |
| errorMessage | string? | Error message if discovery failed |

**Discovery Method Enum**:
- `ExplicitPath`: User provided via CLI/env var
- `VsWhere`: Found via vswhere utility
- `CommonPath`: Found at well-known installation path
- `PathEnvironment`: Found via PATH environment variable
- `NotFound`: All strategies failed

**State Transitions**:
```text
Start → TryExplicitPath → TryVsWhere → TryCommonPaths → TryPathEnv → [Success | NotFound]
```

---

### 2.2 VSWhere Query Schema

**Entity**: `VsWhereQuery`

| Field | Type | Value | Purpose |
|-------|------|-------|---------|
| requires | string | "Microsoft.Component.MSBuild" | For msbuild discovery |
| requires | string | "Microsoft.VisualStudio.Component.TestTools" | For vstest discovery |
| find | string | "MSBuild\\**\\Bin\\MSBuild.exe" | For msbuild |
| find | string | "**\\vstest.console.exe" | For vstest |
| latest | flag | true | Get newest VS installation |

**Output**: Absolute path to tool (one per line if multiple matches)

---

## 3. Test DLL Discovery Schema

### 3.1 Test Assembly Criteria

**Entity**: `TestAssemblyCriteria`

| Criterion | Type | Pattern | Description |
|-----------|------|---------|-------------|
| rootDirectory | string (path) | "TestSRC" | Base directory for recursive search |
| nameFilter | regex | `.*_Test\.dll$` | Filename must end with "_Test.dll" |
| pathFilter | regex | `/bin/Debug/net10\.0-windows[^/]*/win-x64/` | Full path must match pattern |
| exclusionList | string[] | ["CppCliModuleTest.dll"] | Filenames to exclude |
| caseSensitive | bool | false | Windows default |

**Matching Algorithm**:
```text
1. Enumerate all files recursively under rootDirectory
2. Filter by nameFilter (ends with _Test.dll)
3. Filter by pathFilter (contains /bin/Debug/net10.0-windows.*/win-x64/)
4. Exclude any file in exclusionList
5. Return list of absolute paths
```

---

### 3.2 Test Assembly Discovery Result

**Entity**: `TestAssemblyDiscoveryResult`

| Field | Type | Description |
|-------|------|-------------|
| discoveredDlls | string[] | Absolute paths to test DLLs (after all filters) |
| excludedDlls | string[] | DLLs excluded by exclusion list (for logging) |
| totalScanned | int | Total files scanned (for performance metrics) |
| searchRoot | string | Absolute path to TestSRC directory |
| discoveryTimeMs | long | Time taken to discover (for logging) |

**Example Output**:
```json
{
  "discoveredDlls": [
    "D:\\Repos\\App\\TestSRC\\MainAppTest\\bin\\Debug\\net10.0-windows\\win-x64\\MainAppTest_Test.dll",
    "D:\\Repos\\App\\TestSRC\\FeatureTest\\bin\\Debug\\net10.0-windows.7.0\\win-x64\\Feature_Test.dll"
  ],
  "excludedDlls": [
    "CppCliModuleTest.dll"
  ],
  "totalScanned": 45,
  "searchRoot": "D:\\Repos\\App\\TestSRC",
  "discoveryTimeMs": 127
}
```

---

## 4. Execution State Model

### 4.1 Build Execution State

**Entity**: `BuildExecutionState`

| Field | Type | Description |
|-------|------|-------------|
| msbuildPath | string | Resolved path to msbuild.exe |
| solutionPath | string | Absolute path to solution/project file |
| arguments | string[] | msbuild arguments (from passthrough) |
| commandLine | string | Full command line (for logging) |
| exitCode | int? | Exit code from msbuild (null before execution) |
| startTime | DateTime | Execution start timestamp |
| endTime | DateTime? | Execution end timestamp |
| durationMs | long? | Execution duration in milliseconds |

**State Lifecycle**:
```text
Initialized → Executing → [Completed | Failed]
```

---

### 4.2 Test Execution State

**Entity**: `TestExecutionState`

| Field | Type | Description |
|-------|------|-------------|
| vstestPath | string | Resolved path to vstest.console.exe |
| testAssemblies | string[] | Discovered test DLL paths |
| arguments | string[] | vstest arguments (from passthrough) |
| commandLine | string | Full command line (for logging) |
| exitCode | int? | Exit code from vstest (null before execution) |
| startTime | DateTime | Execution start timestamp |
| endTime | DateTime? | Execution end timestamp |
| durationMs | long? | Execution duration in milliseconds |

**State Lifecycle**:
```text
Initialized → DiscoveringDlls → Executing → [Completed | Failed | Skipped]
```

**Skipped State**: Entered when no test DLLs discovered (not an error).

---

### 4.3 Overall Script Execution State

**Entity**: `ScriptExecutionState`

| Field | Type | Description |
|-------|------|-------------|
| target | TargetArgument | Requested target (Build/Test/BuildAndTest) |
| buildState | BuildExecutionState? | Build execution state (if Build or BuildAndTest) |
| testState | TestExecutionState? | Test execution state (if Test or BuildAndTest) |
| finalExitCode | int | Exit code returned to caller (last tool's exit code) |
| totalDurationMs | long | Total script execution time |
| errors | string[] | Script-level errors (tool not found, invalid args) |

**Exit Code Rules**:
- **Build target**: Return msbuild exit code
- **Test target**: Return vstest exit code
- **BuildAndTest target**: If msbuild fails, return its exit code immediately. If msbuild succeeds, return vstest exit code.

---

## 5. Error Handling Schema

### 5.1 Error Types

**Entity**: `ScriptError`

| Field | Type | Description |
|-------|------|-------------|
| errorType | enum | Category of error |
| message | string | Human-readable error message |
| context | map<string, string> | Additional context (paths tried, etc.) |
| exitCode | int | Exit code to return (non-zero) |

**Error Type Enum**:
- `ToolNotFound`: msbuild or vstest not discoverable
- `InvalidArgument`: Missing required argument (e.g., --solution)
- `FileNotFound`: Specified file doesn't exist (e.g., solution path)
- `DirectoryNotFound`: TestSRC directory doesn't exist
- `ProcessFailed`: Tool process returned non-zero exit code

**Example Error Messages**:
```text
ToolNotFound:
  "msbuild not found. Install Visual Studio Build Tools or set MSBUILD_PATH environment variable."
  Context: { "attemptedPaths": ["C:\\Program Files\\...", "PATH"] }

InvalidArgument:
  "Build target requires --solution argument."
  Context: { "target": "Build", "providedArgs": ["--target=Build"] }

FileNotFound:
  "Solution file not found: D:\\Repos\\App\\NotExist.sln"
  Context: { "providedPath": "D:\\Repos\\App\\NotExist.sln" }
```

---

## 6. Logging Schema

### 6.1 Log Entry

**Entity**: `LogEntry`

| Field | Type | Description |
|-------|------|-------------|
| timestamp | DateTime | When log entry was created |
| level | enum | INFO, WARNING, ERROR |
| message | string | Log message content |
| context | map<string, object> | Structured context data |

**Log Level Semantics**:
- **INFO**: Normal operation (tool discovered, command executed)
- **WARNING**: Unexpected but not failing (no test DLLs found)
- **ERROR**: Failure condition (tool not found, process failed)

**Log Output**:
- INFO, WARNING → stdout
- ERROR → stderr

**Example Log Entries**:
```text
[INFO] Resolved msbuild: C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
[INFO] Executing: msbuild "SRC\Apps\Apps.slnx" /p:Configuration=Release /m:4
[WARNING] No test assemblies found matching criteria. Skipping test execution.
[ERROR] vstest.console.exe not found. Install Visual Studio Test Platform or set VSTEST_PATH.
```

---

## 7. Validation Rules

### 7.1 Argument Validation

| Rule ID | Condition | Error Message |
|---------|-----------|---------------|
| VAL-001 | target=Build and solution is null | "Build target requires --solution argument." |
| VAL-002 | solution specified and file doesn't exist | "Solution file not found: {path}" |
| VAL-003 | msbuild-path specified and file doesn't exist | "Specified msbuild path not found: {path}" |
| VAL-004 | vstest-path specified and file doesn't exist | "Specified vstest path not found: {path}" |
| VAL-005 | test-src-path specified and directory doesn't exist | "Test source directory not found: {path}" |

### 7.2 Discovery Validation

| Rule ID | Condition | Behavior |
|---------|-----------|----------|
| VAL-006 | TestSRC directory doesn't exist | ERROR: "TestSRC directory not found at {path}" |
| VAL-007 | No test DLLs discovered | WARNING: "No test assemblies found. Skipping tests." (exit 0) |
| VAL-008 | msbuild not found after all strategies | ERROR: "msbuild not found. Install VS Build Tools or set MSBUILD_PATH." |
| VAL-009 | vstest not found after all strategies | ERROR: "vstest.console.exe not found. Install VS Test Platform or set VSTEST_PATH." |

---

## 8. File Path Conventions

### 8.1 Path Normalization

**Rule**: All paths stored in state are **absolute paths** with **native separators** (backslash on Windows).

| Input Type | Normalization Method | Example |
|------------|---------------------|---------|
| Relative path | Resolve from CWD | `TestSRC` → `D:\Repos\App\TestSRC` |
| Absolute path | Use as-is | `D:\Repos\App\TestSRC` → `D:\Repos\App\TestSRC` |
| Forward slash | Convert to native | `SRC/Apps/App.slnx` → `SRC\Apps\App.slnx` (Windows) |

### 8.2 Path Comparison

**Rule**: All path comparisons use **case-insensitive** logic on Windows.

```csharp
bool PathsEqual(string path1, string path2) {
    var normalized1 = Path.GetFullPath(path1);
    var normalized2 = Path.GetFullPath(path2);
    return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
}
```

---

## 9. State Persistence

**Note**: This is a stateless script. No persistence required.

- **No Configuration Files**: All configuration via CLI arguments or environment variables
- **No State Files**: Each execution is independent
- **No Caching**: Tool paths resolved fresh each run (ensures accuracy if VS updates)

**Rationale**: Simplicity and reliability. Stateless design avoids stale cache issues.

---

## 10. Schema Versioning

**Current Version**: 1.0

**Compatibility Promise**:
- CLI argument names are stable (semver MAJOR bump if removed/renamed)
- Passthrough arguments are never interpreted (no breaking changes possible)
- Error message text is not part of contract (can change without version bump)

**Future Additions** (non-breaking):
- Additional targets (e.g., `Clean`, `Restore`)
- Additional tool path overrides (e.g., `--dotnet-path`)
- Additional discovery filters (e.g., `--test-dll-pattern`)

---

**Status**: Data model complete  
**Next Steps**: Generate contracts/ directory with detailed interface specifications
