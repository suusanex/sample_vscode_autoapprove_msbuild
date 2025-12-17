# Implementation Plan: Unified Build and Test Execution Script

**Branch**: `001-unified-build-script` | **Date**: December 16, 2025 | **Spec**: [spec.md](./spec.md)

## Summary

Create a single-entry-point build and test execution system using Cake (C#) that works identically on local development machines and CI environments. The system provides transparent passthrough of msbuild and vstest.console.exe arguments, automatic test DLL discovery with exclusion rules, and intelligent tool location strategies. This establishes a stable command interface for GitHub Copilot terminal approval rules.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0+ (Cake.Tool requires .NET runtime)  
**Primary Dependencies**: Cake.Sdk (latest), Cake.Core, Cake.Common, Cake.FileHelpers  
**Storage**: N/A (script execution only)  
**Testing**: Unit tests for discovery logic (xUnit), Integration tests via actual build/test execution  
**Target Platform**: Windows 10+ (Visual Studio/Build Tools environment)  
**Project Type**: Build automation script (single entry point)  
**Performance Goals**: Tool discovery <2s, minimal overhead over direct msbuild/vstest invocation  
**Constraints**: Must work with VS 2022 17.x+, must not modify or interpret msbuild/vstest arguments  
**Scale/Scope**: 1-2 solution files, ~10-50 test assemblies per run

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Note**: Constitution template is empty/placeholder. Proceeding with general software engineering best practices:
- ✅ **Single Responsibility**: Entry script only orchestrates; no business logic
- ✅ **Testability**: Discovery logic is unit-testable; tool execution is integration-testable
- ✅ **Transparency**: All msbuild/vstest arguments passed without interpretation
- ✅ **Error Handling**: Clear error messages when tools not found or invalid inputs provided

## Architecture Design

### 1. CLI Entry Point Design

#### Command Structure

The Cake script exposes three primary targets accessible via `dotnet cake`:

```text
dotnet cake --target=Build [--solution=<path>] [-- <msbuild-args>...]
dotnet cake --target=Test [-- <vstest-args>...]
dotnet cake --target=BuildAndTest [--solution=<path>] [-- <msbuild-args> -- <vstest-args>]
```

**Target Definitions**:
- `Build`: Locates msbuild and builds the specified solution/project
- `Test`: Discovers test DLLs and executes vstest.console.exe
- `BuildAndTest`: Sequential execution of Build then Test
- `Default`: Alias for BuildAndTest

#### Argument Separation Strategy

To avoid conflicts between Cake arguments, msbuild arguments, and vstest arguments:

**Double-Dash Separator Pattern** (Selected):
```text
dotnet cake --target=Build --solution=Apps.slnx -- /p:Configuration=Release /m:4
dotnet cake --target=Test -- /TestCaseFilter:"Priority=1" /Logger:trx
dotnet cake --target=BuildAndTest --solution=Apps.slnx -- /p:Configuration=Release -- /Parallel
```

- Everything before first `--` → Cake named parameters
- Everything between first `--` and second `--` (or EOF) → msbuild arguments
- Everything after second `--` → vstest arguments

**Collision Prevention**:
- Cake parameters use `--name=value` syntax (named)
- Tool arguments are collected as positional after separator
- No interpretation or validation of tool arguments (transparent passthrough)

#### Exit Code Policy

**Rule**: The script MUST return the exit code of the last tool executed:
- `Build` target → return msbuild exit code
- `Test` target → return vstest.console.exe exit code
- `BuildAndTest` target → if msbuild fails (non-zero), return immediately; else return vstest exit code

**Implementation**: Use `StartProcess()` with error handling to capture and propagate exit codes.

#### Logging Policy

**Visibility Requirements**:
1. **Tool Discovery**: Log the resolved path to msbuild/vstest.console.exe (helps debugging path issues)
2. **Command Echo**: Log the exact command line being executed (enables manual reproduction)
3. **Passthrough Output**: All stdout/stderr from msbuild/vstest flows through unchanged
4. **Errors**: Script-level errors (tool not found, invalid args) → stderr with ERROR prefix

**Format**:
```text
[INFO] Resolved msbuild: C:\Program Files\...\msbuild.exe
[INFO] Executing: msbuild "Apps.slnx" /p:Configuration=Release /m:4
<msbuild output flows here>
[ERROR] msbuild not found in standard locations
```

### 2. MSBuild Execution Design

#### Tool Discovery Strategy (Priority Order)

**Search Order**:
1. **Explicit Path Override** (Highest Priority)
   - Environment variable: `MSBUILD_PATH`
   - Cake parameter: `--msbuild-path=<path>`
   - If specified and exists → use it
   - If specified and doesn't exist → ERROR (fail fast)

2. **Visual Studio Installation Discovery** (via vswhere)
   - Execute: `vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`
   - Returns: Full path to latest VS installation's msbuild
   - Advantages: Finds newest toolset, respects VS installation
   - Availability: vswhere.exe ships with VS 2017+ at `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe`

3. **PATH Resolution** (Fallback)
   - Use `dotnet msbuild` (cross-platform if needed)
   - Or resolve `msbuild` from PATH via `where msbuild`
   - Advantages: Works on machines with minimal setup

**Error Handling**:
- If none found → ERROR: "msbuild not found. Install Visual Studio Build Tools or set MSBUILD_PATH."
- Log all attempted paths for debugging

#### Argument Passthrough Specification

**Core Principle**: Zero Interpretation

The script collects all arguments after the first `--` separator and passes them to msbuild **in the exact order received, with no modifications**:

```csharp
// Pseudocode
var msbuildArgs = ArgumentsAfterFirstSeparator; // e.g., ["/p:Configuration=Release", "/m:4"]
var command = $"{msbuildPath} \"{solutionPath}\" {string.Join(" ", msbuildArgs)}";
```

**Rationale**:
- Copilot can dynamically add filters, properties, or logging without script changes
- Order preservation ensures compatibility with positional-sensitive tools
- No validation means future msbuild versions work automatically

**Solution Path Handling**:
- Cake parameter `--solution=<path>` is inserted as the **first positional argument** to msbuild
- User msbuild args appended after
- If `--solution` not provided and `Build` target invoked → ERROR

#### Cake MSBuild Integration Selection

**Option A: Cake.Common.Tools.MSBuild Aliases**
- Pros: Typed API, built-in logging, automatic path resolution
- Cons: May transform arguments, harder to guarantee transparent passthrough

**Option B: Direct Process Execution** (Selected)
- Pros: Full control over arguments, guaranteed passthrough, explicit command visibility
- Cons: Manual path resolution, manual exit code handling

**Selected: Option B - Direct Process Execution**

**Implementation**:
```csharp
var exitCode = StartProcess(msbuildPath, new ProcessSettings {
    Arguments = ProcessArgumentBuilder
        .FromString($"\"{solutionPath}\"")
        .Append(string.Join(" ", msbuildArgs)),
    RedirectStandardOutput = false, // Let output flow through
    RedirectStandardError = false
});
```

### 3. VSTest Execution Design

#### Tool Discovery Strategy (Priority Order)

**Search Order**:
1. **Explicit Path Override**
   - Environment variable: `VSTEST_PATH`
   - Cake parameter: `--vstest-path=<path>`
   - Behavior: Same as msbuild (fail fast if specified and missing)

2. **Visual Studio Installation Discovery** (via vswhere)
   - Execute: `vswhere -latest -requires Microsoft.VisualStudio.Component.TestTools -find **\vstest.console.exe`
   - Returns: Path to vstest.console.exe in latest VS installation
   - Note: Test tools are optional VS component

3. **Common Installation Paths** (Manual Check)
   - VS 2022: `C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe`
   - VS 2022 Community: Replace `Enterprise` with `Community`
   - Check all variants: Enterprise, Professional, Community, BuildTools

4. **PATH Resolution**
   - `where vstest.console.exe`

**Error Handling**:
- If not found → ERROR: "vstest.console.exe not found. Install Visual Studio Test Platform or set VSTEST_PATH."

#### Argument Passthrough Specification

**Core Principle**: Transparent Extension

All vstest arguments collected after the second `--` separator (or first if `Test` target) are passed to vstest.console.exe **without modification**:

```csharp
// Pseudocode
var vstestArgs = ArgumentsAfterSecondSeparator; // e.g., ["/TestCaseFilter:Priority=1", "/Parallel"]
var dllPaths = DiscoverTestDlls(); // ["path/to/Test1.dll", "path/to/Test2.dll"]
var command = $"{vstestPath} {string.Join(" ", dllPaths.Select(p => $"\"{p}\""))} {string.Join(" ", vstestArgs)}";
```

**DLL Arguments First**: Test DLL paths are positioned **before** user vstest arguments to ensure vstest positional parsing works correctly.

**Order Preservation**: User arguments maintain their order (important for logger configurations).

### 4. Test DLL Discovery Logic Design

#### Discovery Root

**Path**: `TestSRC` directory (relative to repository root)

**Resolution**:
- Repository root = directory containing `.git` folder (detect via `DirectoryInfo.Parent` traversal)
- Alternatively: Use Cake's built-in `MakeAbsolute(Directory("./TestSRC"))` from script location
- Explicit override: `--test-src-path=<path>` parameter

**Error Handling**:
- If TestSRC doesn't exist → ERROR: "TestSRC directory not found at <resolved-path>"

#### File Enumeration

**Algorithm**:
1. Recursively enumerate all files under TestSRC
2. Filter: Name ends with `_Test.dll` (case-insensitive on Windows)
3. Filter: Full path matches pattern `\bin\Debug\net10.0-windows.*\win-x64\`
4. Exclude: Files named exactly `CppCliModuleTest.dll` (case-insensitive)

**Implementation Considerations**:

**Path Pattern Matching**:
```csharp
bool MatchesTargetPath(string fullPath) {
    // Normalize to forward slashes for consistent matching
    var normalized = fullPath.Replace('\\', '/');
    
    // Pattern: /bin/Debug/net10.0-windows.*/win-x64/
    // Regex: /bin/Debug/net10\.0-windows[^/]*/win-x64/
    return Regex.IsMatch(normalized, @"/bin/Debug/net10\.0-windows[^/]*/win-x64/", 
        RegexOptions.IgnoreCase);
}
```

**Exclusion Check**:
```csharp
bool IsExcluded(FileInfo file) {
    return file.Name.Equals("CppCliModuleTest.dll", StringComparison.OrdinalIgnoreCase);
}
```

**Windows Path Handling**:
- Use `Path.DirectorySeparatorChar` for cross-platform script potential (though target is Windows)
- Case-insensitive comparisons via `StringComparison.OrdinalIgnoreCase`
- Handle spaces in paths via proper quoting when passing to vstest

#### Discovery Results Logging

**Visibility**:
```text
[INFO] Discovering test DLLs in: D:\Data\git\repo\TestSRC
[INFO] Found 3 test assemblies:
  - D:\Data\git\repo\TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll
  - D:\Data\git\repo\TestSRC\OtherTest\bin\Debug\net10.0-windows.7.0\win-x64\OtherModule_Test.dll
  - D:\Data\git\repo\TestSRC\Feature_Test\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Feature_Test.dll
[INFO] Excluded 1 assembly:
  - CppCliModuleTest.dll
```

**Error Case**:
- If no DLLs found → WARNING: "No test assemblies matched discovery criteria. Skipping test execution." (exit 0, not an error)

### 5. CI Integration Design

#### Shared Entry Point Contract

**Command Stability**:
- Same command works locally and in Azure Pipelines:
  ```yaml
  - task: PowerShell@2
    inputs:
      script: 'dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx'
  ```

**Prerequisites Verification** (Script Responsibility):
- Check .NET SDK installed (`dotnet --version`)
- Check Cake.Tool available (`dotnet tool list cake.tool`)
- If Cake not found → ERROR with install instruction: "Run: dotnet tool install --global Cake.Tool"

#### Environment Differences Handled

**Local Development**:
- Interactive terminal (colored output supported)
- Full Visual Studio typically installed (msbuild/vstest readily available)
- Manual invocation with varied arguments

**CI Environment (Azure Pipelines)**:
- Non-interactive (no color codes)
- VS Build Tools or minimal install (rely on vswhere/explicit paths)
- Scripted invocation with consistent arguments

**Design Accommodations**:
- Auto-detect terminal capabilities via `Console.IsOutputRedirected`
- Accept explicit tool paths via environment variables (CI can set `MSBUILD_PATH`, `VSTEST_PATH`)
- Return correct exit codes (CI interprets 0=success, non-zero=failure)

#### Copilot Terminal Approval Benefits

**Current Problem**:
- Copilot may generate varied msbuild commands (different paths, different argument orders)
- User must approve each variation individually
- Approval rules become complex (whitelist many patterns)

**Solution via Single Entry Point**:
- **Stable Command**: Always `dotnet cake --target=...`
- **Approval Rule**: Whitelist `dotnet cake` (single pattern)
- **Argument Variance**: All variability isolated to passthrough arguments (after `--`)
- **Security**: Script validated once; arguments are domain-specific (build/test options), not system commands

**User Experience Improvement**:
- First-time approval of `dotnet cake` script
- Subsequent invocations with different arguments → no new approval needed (same script)
- Reduced approval fatigue

## Project Structure

### Documentation (this feature)

```text
specs/001-unified-build-script/
├── plan.md              # This file (architectural design)
├── research.md          # Phase 0: Technology decisions and rationale
├── data-model.md        # Phase 1: CLI argument schema, discovery logic schema
├── quickstart.md        # Phase 1: Getting started guide
└── contracts/           # Phase 1: Input/output contracts
    ├── cli-interface.md
    └── discovery-spec.md
```

### Source Code (repository root)

```text
build/
├── build.cake           # Main Cake script with targets
├── tools/               # Cake tool discovery helpers (optional refactor)
│   ├── msbuild.cake
│   └── vstest.cake
└── discovery/           # Test DLL discovery logic (optional refactor)
    └── testdlls.cake

.config/
└── dotnet-tools.json    # Manifest for Cake.Tool (enables `dotnet tool restore`)

build.sh                 # Cross-platform Cake bootstrapper (future)
build.ps1                # PowerShell Cake bootstrapper (Windows, optional)
```

**Structure Decision**: 
- **Single-file approach** initially: All logic in `build/build.cake` (simplicity for 200-300 LOC)
- **Future refactor**: If complexity exceeds ~400 LOC, split into modular .cake files (tool/, discovery/)
- **Bootstrapper**: Use `dotnet cake` directly (requires Cake.Tool installed); bootstrappers (build.ps1/build.sh) are optional convenience

## Complexity Tracking

No constitution violations detected. All design decisions follow standard build automation practices:

| Aspect | Justification |
|--------|---------------|
| Cake over PowerShell | C# provides better testability, cross-platform potential, and type safety for path manipulation |
| Direct process execution over Cake aliases | Guarantees transparent argument passthrough (core requirement) |
| Regex for path matching | Accommodates variable Windows SDK versions (net10.0-windows.X.Y.Z) in path pattern |

## Non-Goals (Explicit Out-of-Scope)

To maintain simplicity and single-responsibility:

1. **Build Orchestration Logic**: No custom build steps, pre/post-build actions, or conditional compilation
2. **Test Filtering Business Logic**: No test categorization or suite selection; rely on vstest's `/TestCaseFilter`
3. **Artifact Management**: No publishing, packaging, or deployment; script returns after build/test
4. **Parallel Execution Management**: No custom parallelization; rely on msbuild `/m` and vstest `/Parallel`
5. **Result Parsing**: No interpretation of test results; vstest output and exit code are authoritative
6. **Configuration Management**: No environment-specific config files; use passthrough arguments or env vars
7. **Dependency Resolution**: No NuGet restore logic; assume `dotnet restore` or VS has handled it

## Success Criteria Mapping

| Requirement ID | Design Component | Verification Method |
|---------------|------------------|---------------------|
| FR-001 | `--solution` parameter, inserted as first msbuild arg | Unit test: argument builder |
| FR-002 | vswhere → VS installation → PATH discovery chain | Integration test: tool resolution |
| FR-003, FR-004 | Arguments after first `--` passed unmodified | Integration test: echo command, verify output |
| FR-005 | `StartProcess` exit code capture and return | Integration test: deliberate build failure |
| FR-006, FR-007 | Recursive TestSRC enumeration, `_Test.dll` filter | Unit test: mock file system |
| FR-008 | Regex path pattern match | Unit test: pattern validation |
| FR-009 | `CppCliModuleTest.dll` exclusion check | Unit test: exclusion logic |
| FR-010 | vswhere → VS installation → PATH discovery for vstest | Integration test: tool resolution |
| FR-011, FR-012 | Arguments after second `--` passed unmodified | Integration test: echo command |
| FR-013 | `StartProcess` exit code from vstest | Integration test: deliberate test failure |
| FR-014 | Same command syntax locally and in CI | CI pipeline test: replicate local command |
| FR-015, FR-016 | ERROR messages with resolution hints | Integration test: missing tool scenario |
| FR-017 | No business logic in script (orchestration only) | Code review: single-responsibility check |

## Implementation Phases Summary

**Phase 0 (Research)**: Investigate Cake best practices, vswhere usage patterns, .NET process execution APIs  
**Phase 1 (Design)**: This document defines WHAT and HOW at design level; contracts specify CLI schema  
**Phase 2 (Tasks)**: Not covered here; task breakdown will decompose implementation into testable increments

---

**Last Updated**: December 16, 2025  
**Status**: Phase 1 In Progress - Constitution Check complete, Architecture Design complete
