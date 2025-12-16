# Research: Unified Build and Test Execution Script

**Date**: December 16, 2025  
**Feature**: 001-unified-build-script

## Overview

This document consolidates research findings for technology choices, patterns, and best practices required to implement the unified build and test execution script using Cake.

## Research Tasks

### 1. Cake Build Automation Framework

#### Decision: Use Cake.Tool with .NET 8.0+

**Rationale**:
- **C# Familiarity**: Leverages existing C# expertise for build scripting (vs. learning Bash/PowerShell DSLs)
- **Type Safety**: Strong typing reduces runtime errors in path manipulation and argument handling
- **Cross-Platform**: Cake supports Windows, Linux, macOS (future extensibility)
- **Ecosystem**: Rich plugin ecosystem (Cake.Common, Cake.FileHelpers) for common tasks
- **Testing**: C# build logic can be unit-tested using standard xUnit/NUnit frameworks

**Alternatives Considered**:
1. **PowerShell Scripts**
   - Pros: Native Windows integration, no additional tooling
   - Cons: Harder to unit test, limited type safety, Windows-only
   - Rejected: Testability is critical for complex discovery logic

2. **FAKE (F# Make)**
   - Pros: Functional paradigm, similar ecosystem to Cake
   - Cons: Team unfamiliar with F#, smaller community
   - Rejected: Learning curve outweighs benefits for this use case

3. **Direct MSBuild Targets**
   - Pros: No external dependencies, integrated with VS
   - Cons: XML-based (verbose), harder to express complex logic
   - Rejected: Test discovery logic would be overly complex in MSBuild XML

**Version Selection**: Cake.Tool 4.x (latest stable as of Dec 2025)
- Requires .NET 8.0 SDK or later
- Installed via `dotnet tool install --global Cake.Tool`

**Key Features Used**:
- `StartProcess()`: Direct process execution with exit code capture
- `MakeAbsolute()`: Path resolution relative to script location
- `GetFiles()`: Recursive file enumeration with glob patterns
- `Argument()`: CLI argument parsing

---

### 2. vswhere Tool for Visual Studio Discovery

#### Decision: Use vswhere for msbuild and vstest.console.exe location

**Rationale**:
- **Reliability**: Official Microsoft tool for locating VS installations
- **Version Awareness**: Automatically finds latest installed version
- **Component Filtering**: Can filter by required workloads (MSBuild, TestTools)
- **Stability**: Part of VS installer since 2017, well-maintained

**Usage Patterns**:

**Locate MSBuild**:
```powershell
vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
```
- `-latest`: Returns only the newest VS installation
- `-requires`: Filters installations that have MSBuild component
- `-find`: Returns file path matching pattern

**Locate vstest.console.exe**:
```powershell
vswhere -latest -products * -requires Microsoft.VisualStudio.Component.TestTools -find **\vstest.console.exe
```
- `-products *`: Includes all VS editions (Enterprise, Professional, Community, BuildTools)
- Test tools are optional component, may not be in all installations

**Availability Check**:
- Standard location: `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe`
- Ships with VS 2017 Update 2 and later
- Also available as standalone from [GitHub releases](https://github.com/microsoft/vswhere)

**Error Handling**:
- If vswhere not found → fallback to PATH resolution
- If vswhere returns empty → ERROR with installation instructions

**Alternatives Considered**:
1. **Registry Search** (pre-VS2017 approach)
   - Cons: Registry keys vary by VS version, unreliable with VS2017+
   - Rejected: vswhere is the official replacement

2. **Hardcoded Paths**
   - Cons: Breaks when VS updates, assumes specific edition
   - Rejected: Too brittle for CI/local flexibility

---

### 3. Argument Passthrough Patterns

#### Decision: Double-Dash Separator with Order Preservation

**Pattern**:
```text
dotnet cake --target=Build --solution=path.slnx -- /p:Configuration=Release /m:4
              ↑ Cake args ↑                      ↑ Tool args (msbuild) ↑
```

**Rationale**:
- **Convention**: `--` is widely used separator (Git, npm, pytest use same pattern)
- **Unambiguous**: Clear boundary between script args and tool args
- **Order Preservation**: Tool args collected as array, passed verbatim
- **No Parsing**: Script doesn't interpret tool args (transparent passthrough)

**Implementation in Cake**:
```csharp
// Collect all arguments after first "--"
var allArgs = Context.Arguments.GetArguments();
var separatorIndex = Array.IndexOf(allArgs, "--");
var toolArgs = (separatorIndex >= 0) 
    ? allArgs.Skip(separatorIndex + 1).ToList() 
    : new List<string>();
```

**Best Practices from Cake Community**:
- Use `HasArgument()` for boolean flags
- Use `Argument<T>()` for typed parameters (e.g., `--solution=<path>`)
- Use `GetArguments()` for raw positional args after separator

**Edge Cases Handled**:
- Multiple `--` separators: First one delimits Cake args, second one (if present) delimits msbuild vs vstest args
- No `--` provided: Tool args array is empty (valid for BuildAndTest with defaults)
- Quoted arguments: Preserved through `string.Join(" ", toolArgs)` (quotes maintained)

---

### 4. .NET Process Execution APIs

#### Decision: Use Cake's `StartProcess()` with ProcessSettings

**Rationale**:
- **Exit Code Capture**: Returns int exit code directly (critical for FR-005, FR-013)
- **Output Streaming**: Can redirect or passthrough stdout/stderr
- **Argument Builder**: `ProcessArgumentBuilder` handles escaping/quoting
- **Cross-Platform**: Works on Windows, Linux, macOS

**Implementation Pattern**:
```csharp
var exitCode = StartProcess(toolPath, new ProcessSettings {
    Arguments = argumentBuilder,
    WorkingDirectory = repositoryRoot,
    RedirectStandardOutput = false, // Let tool output flow directly
    RedirectStandardError = false
});

if (exitCode != 0) {
    throw new Exception($"Tool exited with code {exitCode}");
}
```

**Output Handling Options**:
1. **Passthrough** (Selected for Build/Test):
   - `RedirectStandardOutput = false`
   - Output goes directly to console (CI/local see same output)
   
2. **Capture** (For tool discovery):
   - `RedirectStandardOutput = true`
   - Process output via `ProcessSettings.OutputDataReceived`
   - Use for vswhere output parsing

**Alternatives Considered**:
1. **Cake's MSBuild/VSTest Aliases**
   - Pros: Simpler API, built-in defaults
   - Cons: May modify arguments, harder to verify passthrough
   - Rejected: Transparent passthrough is non-negotiable

2. **System.Diagnostics.Process**
   - Pros: Full control, no dependencies
   - Cons: More boilerplate, manual path handling
   - Rejected: Cake's wrapper provides same control with less code

---

### 5. Windows Path Handling and Regex Patterns

#### Decision: Normalize to forward slashes, case-insensitive regex

**Pattern for Test DLL Discovery**:
```text
Path must match: \bin\Debug\net10.0-windows.*\win-x64\
Example matches:
  - TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll
  - TestSRC\OtherTest\bin\Debug\net10.0-windows.7.0\win-x64\Other_Test.dll
  - TestSRC\Feature\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Feature_Test.dll
```

**Regex Implementation**:
```csharp
var pattern = @"/bin/Debug/net10\.0-windows[^/]*/win-x64/";
var normalized = fullPath.Replace('\\', '/'); // D:\...\bin\... → D:/.../bin/...
var matches = Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase);
```

**Rationale**:
- **Variable SDK Versions**: `net10.0-windows.*` captures versioned SDK paths (e.g., net10.0-windows10.0.19041.0)
- **Forward Slash Normalization**: Simplifies regex (don't need to escape backslashes)
- **Case-Insensitive**: Windows file system is case-insensitive; match behavior

**Edge Cases Handled**:
- Drive letters: Preserved in normalization (C:\ → C:/)
- UNC paths: Work with forward slash normalization (\\server\share → //server/share)
- Trailing slashes: Regex expects slash after win-x64 (DLL is child of that directory)

**Best Practices from .NET Community**:
- Always use `Path.Combine()` for cross-platform path building
- Use `StringComparison.OrdinalIgnoreCase` for filename comparisons
- Use `Path.GetFullPath()` to resolve relative paths before matching

---

### 6. CI Integration Patterns (Azure Pipelines)

#### Decision: Use PowerShell task with `dotnet cake` invocation

**Pattern**:
```yaml
# azure-pipelines.yml
steps:
- task: UseDotNet@2
  inputs:
    packageType: 'sdk'
    version: '8.x'
    
- task: PowerShell@2
  displayName: 'Build and Test'
  inputs:
    script: |
      dotnet tool restore
      dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx
    failOnStderr: false # msbuild/vstest may write to stderr without failing
```

**Rationale**:
- **Consistency**: Exact same command works locally and in CI
- **Tooling**: `dotnet tool restore` ensures Cake.Tool available from .config/dotnet-tools.json
- **Exit Code**: PowerShell task fails if `dotnet cake` returns non-zero (automatic)

**Environment Variables for Tool Paths**:
```yaml
- task: PowerShell@2
  env:
    MSBUILD_PATH: 'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
    VSTEST_PATH: 'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
  inputs:
    script: 'dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx'
```

**Best Practices**:
- Pin .NET SDK version via `global.json` (ensures consistent tooling)
- Use `dotnet tool restore` instead of global install (per-repo isolation)
- Set `failOnStderr: false` (many tools emit warnings to stderr)

**Alternatives Considered**:
1. **MSBuild Task + VSTest Task** (Azure Pipelines built-ins)
   - Cons: Two separate tasks, harder to maintain argument consistency
   - Rejected: Defeats purpose of single entry point

2. **Bash Script** (for cross-platform CI)
   - Pros: Works on Linux agents
   - Cons: Cake already handles cross-platform; unnecessary layer
   - Rejected: PowerShell task works on Windows agents (target platform)

---

### 7. Unit Testing Strategy for Cake Scripts

#### Decision: Extract testable logic into C# libraries, integration test Cake targets

**Pattern**:
```text
build/
├── build.cake              # Orchestration only (thin layer)
└── BuildLogic/             # C# class library
    ├── BuildLogic.csproj
    ├── ToolDiscovery.cs    # Unit-testable: vswhere, PATH resolution
    ├── TestDllFinder.cs    # Unit-testable: file enumeration, filtering
    └── ArgumentParser.cs   # Unit-testable: -- separator parsing

tests/
└── BuildLogic.Tests/
    ├── ToolDiscoveryTests.cs
    ├── TestDllFinderTests.cs
    └── ArgumentParserTests.cs
```

**Rationale**:
- **Separation**: Cake script is thin orchestration; logic in testable C# library
- **Fast Feedback**: Unit tests run in milliseconds (no process spawning)
- **Coverage**: Test edge cases (missing tools, empty directories) without CI setup

**Integration Tests** (separate from unit tests):
```csharp
[Fact]
public void BuildAndTest_WithValidSolution_ReturnsZeroExitCode() {
    // Arrange: Create temp solution with passing tests
    var exitCode = RunCakeScript("--target=BuildAndTest", $"--solution={tempSolution}");
    
    // Assert
    Assert.Equal(0, exitCode);
}
```

**Alternatives Considered**:
1. **All Logic in .cake File**
   - Cons: Hard to unit test (requires Cake runtime)
   - Rejected: Complex discovery logic needs fast test feedback

2. **Integration Tests Only**
   - Cons: Slow, requires full VS installation in test environment
   - Rejected: Want fast unit tests for development

---

## Technology Stack Summary

| Component | Technology | Version | Rationale |
|-----------|-----------|---------|-----------|
| Build Script Language | C# (Cake) | Cake.Tool 4.x | Type safety, testability, cross-platform |
| Runtime | .NET | 8.0+ | Latest LTS, required by Cake.Tool |
| Tool Discovery | vswhere | Latest (ships with VS) | Official VS locator, component-aware |
| Process Execution | Cake StartProcess | Built-in | Exit code capture, output streaming |
| Path Matching | Regex | .NET BCL | Handles variable SDK versions |
| Unit Testing | xUnit | Latest | Industry standard, VS integration |
| CI Platform | Azure Pipelines | N/A | PowerShell task with dotnet cake |

---

## Key Design Patterns Applied

### 1. Transparent Proxy Pattern
- Script acts as transparent proxy between user and tools (msbuild/vstest)
- Zero interpretation of tool arguments (passthrough)

### 2. Strategy Pattern (Tool Discovery)
- Multiple strategies tried in priority order: explicit path → vswhere → PATH
- First successful strategy wins

### 3. Chain of Responsibility (Error Handling)
- Each discovery strategy either succeeds or passes to next
- Final handler emits ERROR if all strategies fail

### 4. Separation of Concerns
- CLI parsing: Cake script
- Tool discovery: Testable C# library (future refactor)
- Test DLL discovery: Testable C# library (future refactor)
- Process execution: Cake StartProcess

---

## Open Questions Resolved

### Q1: Should we use Cake aliases for MSBuild/VSTest?
**A**: No. Direct process execution guarantees transparent argument passthrough (core requirement). Aliases may transform arguments in subtle ways.

### Q2: How to handle SDK version variance in test paths?
**A**: Regex pattern `net10\.0-windows[^/]*` matches any suffix after base version (e.g., .7.0, 10.0.19041.0).

### Q3: What if vswhere is not installed?
**A**: Fallback to PATH resolution. If both fail, ERROR with installation instructions.

### Q4: Should test DLL discovery failures be errors?
**A**: No. If no DLLs found, emit WARNING and skip test execution (exit 0). Allows "build-only" workflows without forcing test projects.

### Q5: How to test Cake scripts themselves?
**A**: Extract logic into C# libraries for unit testing. Integration test Cake targets end-to-end.

---

## References

- [Cake Documentation](https://cakebuild.net/docs/)
- [vswhere GitHub](https://github.com/microsoft/vswhere)
- [Azure Pipelines YAML Schema](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/)
- [MSBuild Command-Line Reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference)
- [VSTest.Console.exe Command-Line Options](https://learn.microsoft.com/en-us/visualstudio/test/vstest-console-options)

---

**Status**: Research complete  
**Next Steps**: Generate data-model.md, contracts/, quickstart.md
