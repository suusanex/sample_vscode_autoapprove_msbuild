# Test DLL Discovery Specification

**Feature**: 001-unified-build-script  
**Version**: 1.0  
**Date**: December 16, 2025

## Overview

This document specifies the algorithm, criteria, and behavior for automatic test DLL discovery. This specification is testable and forms the basis for unit tests.

---

## Discovery Algorithm

### High-Level Flow

```text
1. Resolve TestSRC root directory
2. Enumerate all files recursively
3. Apply filename filter (_Test.dll)
4. Apply path pattern filter (/bin/Debug/net10.0-windows.*/win-x64/)
5. Apply exclusion list (CppCliModuleTest.dll)
6. Return list of absolute paths
```

---

## Discovery Root Resolution

### Default Behavior

**Root Directory**: `TestSRC`

**Resolution Strategy**:
1. Find repository root (directory containing `.git`)
2. Resolve `TestSRC` relative to repository root
3. If doesn't exist → ERROR

**Example**:
```text
Repository Root: D:\Repos\MyApp
TestSRC: D:\Repos\MyApp\TestSRC
```

---

### Override via Argument

**Argument**: `--test-src-path=<path>`

**Resolution**:
- Relative paths → resolved from repository root
- Absolute paths → used as-is

**Examples**:
```bash
# Relative
dotnet cake --target=Test --test-src-path=tests
# Resolves to: D:\Repos\MyApp\tests

# Absolute
dotnet cake --target=Test --test-src-path="D:\CustomTests"
# Uses: D:\CustomTests
```

---

### Error Handling

**Condition**: TestSRC directory doesn't exist

**Behavior**: ERROR with message:
```text
[ERROR] TestSRC directory not found at: <resolved-path>
```

**Exit Code**: 1

---

## File Enumeration

### Recursive Search

**Method**: `Directory.GetFiles(root, "*", SearchOption.AllDirectories)`

**Characteristics**:
- Traverses all subdirectories
- Includes hidden files (if any)
- Follows symlinks (if any)
- Case-insensitive on Windows

**Performance**: Acceptable for ~50-200 test projects (typical repo scale)

---

## Filename Filter

### Pattern

**Criteria**: Filename ends with `_Test.dll`

**Regex**: `.*_Test\.dll$`

**Case Sensitivity**: Case-insensitive on Windows (OrdinalIgnoreCase)

### Matching Examples

| Filename | Matches | Reason |
|----------|---------|--------|
| `MainAppTest_Test.dll` | ✅ | Ends with _Test.dll |
| `Feature_Test.dll` | ✅ | Ends with _Test.dll |
| `Integration_Test.dll` | ✅ | Ends with _Test.dll |
| `TestHelper.dll` | ❌ | Doesn't end with _Test.dll |
| `Test.dll` | ❌ | Missing underscore |
| `MyTest_Tests.dll` | ❌ | Ends with _Tests.dll (plural) |
| `MAINAPPTEST_TEST.DLL` | ✅ | Case-insensitive match |

---

## Path Pattern Filter

### Pattern

**Criteria**: Full path contains `/bin/Debug/net10.0-windows.*/win-x64/`

**Regex**: `/bin/Debug/net10\.0-windows[^/]*/win-x64/`

**Notes**:
- Paths normalized to forward slashes before matching
- `[^/]*` matches any characters except slash (captures SDK version suffix)
- Case-insensitive on Windows

### Matching Examples

| Full Path | Matches | Reason |
|-----------|---------|--------|
| `D:\Repos\App\TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll` | ✅ | Exact match |
| `D:\Repos\App\TestSRC\Feature\bin\Debug\net10.0-windows.7.0\win-x64\Feature_Test.dll` | ✅ | Includes .7.0 suffix |
| `D:\Repos\App\TestSRC\Other\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Other_Test.dll` | ✅ | Includes Windows SDK version |
| `D:\Repos\App\TestSRC\Test\bin\Debug\net8.0\Test_Test.dll` | ❌ | Wrong TFM (net8.0) |
| `D:\Repos\App\TestSRC\Test\bin\Release\net10.0-windows\win-x64\Test_Test.dll` | ❌ | Release config (not Debug) |
| `D:\Repos\App\TestSRC\Test\bin\Debug\net10.0-windows\win-x86\Test_Test.dll` | ❌ | Wrong RID (win-x86) |
| `D:\Repos\App\TestSRC\Test\bin\Debug\net10.0-windows\Test_Test.dll` | ❌ | Missing win-x64 subdirectory |

---

### Path Normalization

**Process**:
```csharp
var normalized = fullPath.Replace('\\', '/');
```

**Before**: `D:\Repos\App\TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll`

**After**: `D:/Repos/App/TestSRC/MainAppTest/bin/Debug/net10.0-windows/win-x64/MainAppTest_Test.dll`

**Rationale**: Simplifies regex (no need to escape backslashes)

---

### Regex Explanation

**Pattern**: `/bin/Debug/net10\.0-windows[^/]*/win-x64/`

| Component | Meaning |
|-----------|---------|
| `/bin/Debug/` | Literal path segment |
| `net10\.0-windows` | Literal TFM prefix (dot escaped) |
| `[^/]*` | Zero or more non-slash characters (SDK version suffix) |
| `/win-x64/` | Literal RID directory |

**Captures**:
- `net10.0-windows` (base TFM)
- `net10.0-windows.7.0` (with patch version)
- `net10.0-windows10.0.19041.0` (with Windows SDK version)

---

## Exclusion List

### Pattern

**Criteria**: Filename equals `CppCliModuleTest.dll`

**Comparison**: Case-insensitive exact match

### Implementation

```csharp
bool IsExcluded(FileInfo file) {
    return file.Name.Equals("CppCliModuleTest.dll", StringComparison.OrdinalIgnoreCase);
}
```

### Exclusion Examples

| Filename | Excluded | Reason |
|----------|----------|--------|
| `CppCliModuleTest.dll` | ✅ | Exact match |
| `CPPCLIMODULETEST.DLL` | ✅ | Case-insensitive match |
| `CppCliModuleTest_Test.dll` | ❌ | Different name (has _Test suffix) |
| `CppCliModule.dll` | ❌ | Different name |

---

### Rationale for Exclusion

**Background**: CppCliModuleTest.dll is a C++/CLI test project that:
- Cannot be executed by vstest.console.exe directly
- Requires special test runner or native harness
- Would cause vstest to fail if included

**Future**: If exclusion list grows, move to configuration file (`.testignore`).

---

## Discovery Results

### Success Case

**Output**: Array of absolute paths (string[])

**Example**:
```json
[
  "D:\\Repos\\App\\TestSRC\\MainAppTest\\bin\\Debug\\net10.0-windows\\win-x64\\MainAppTest_Test.dll",
  "D:\\Repos\\App\\TestSRC\\FeatureTest\\bin\\Debug\\net10.0-windows.7.0\\win-x64\\Feature_Test.dll",
  "D:\\Repos\\App\\TestSRC\\IntegrationTest\\bin\\Debug\\net10.0-windows\\win-x64\\Integration_Test.dll"
]
```

**Order**: Unspecified (may vary by file system)

**Duplicates**: None (file paths are unique)

---

### No DLLs Found

**Output**: Empty array (`[]`)

**Log Message**:
```text
[WARNING] No test assemblies found matching discovery criteria. Skipping test execution.
Search criteria:
  - Root: D:\Repos\App\TestSRC
  - Pattern: *_Test.dll in /bin/Debug/net10.0-windows.*/win-x64/
  - Excluded: CppCliModuleTest.dll
```

**Behavior**: 
- Test execution skipped (not an error)
- Script returns exit code 0
- Rationale: Allow build-only workflows without forcing test projects

---

### Error Case

**Condition**: TestSRC directory doesn't exist

**Log Message**:
```text
[ERROR] TestSRC directory not found at: D:\Repos\App\TestSRC
```

**Exit Code**: 1

---

## Logging Output

### Discovery Start

```text
[INFO] Discovering test DLLs in: D:\Repos\App\TestSRC
```

---

### Discovery Results

**Found DLLs**:
```text
[INFO] Found 3 test assemblies:
  - D:\Repos\App\TestSRC\MainAppTest\bin\Debug\net10.0-windows\win-x64\MainAppTest_Test.dll
  - D:\Repos\App\TestSRC\FeatureTest\bin\Debug\net10.0-windows.7.0\win-x64\Feature_Test.dll
  - D:\Repos\App\TestSRC\IntegrationTest\bin\Debug\net10.0-windows\win-x64\Integration_Test.dll
```

**Excluded DLLs** (if any):
```text
[INFO] Excluded 1 assembly:
  - CppCliModuleTest.dll (full path: D:\Repos\App\TestSRC\CppCliModuleTest\bin\Debug\net10.0-windows\win-x64\CppCliModuleTest.dll)
```

---

### No DLLs Found

```text
[WARNING] No test assemblies found matching discovery criteria. Skipping test execution.
Search criteria:
  - Root: D:\Repos\App\TestSRC
  - Pattern: *_Test.dll in /bin/Debug/net10.0-windows.*/win-x64/
  - Excluded: CppCliModuleTest.dll
Total files scanned: 127
```

---

## Performance Characteristics

### Expected Performance

| Metric | Target | Rationale |
|--------|--------|-----------|
| Discovery Time | < 500ms | File enumeration on local SSD |
| Files Scanned | 100-500 | Typical test project count |
| Memory Usage | < 50MB | In-memory file list |

### Optimization Notes

- No caching (ensures fresh results each run)
- Single-pass enumeration (no multiple traversals)
- Lazy evaluation (could use `yield return` for very large repos)

---

## Unit Test Scenarios

### TC-001: Basic Discovery

**Setup**:
```text
TestSRC/
  MainAppTest/
    bin/Debug/net10.0-windows/win-x64/
      MainAppTest_Test.dll
```

**Expected**: 1 DLL discovered

---

### TC-002: SDK Version Variants

**Setup**:
```text
TestSRC/
  Test1/bin/Debug/net10.0-windows/win-x64/Test1_Test.dll
  Test2/bin/Debug/net10.0-windows.7.0/win-x64/Test2_Test.dll
  Test3/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Test3_Test.dll
```

**Expected**: 3 DLLs discovered

---

### TC-003: Exclusion Filter

**Setup**:
```text
TestSRC/
  CppCliModuleTest/bin/Debug/net10.0-windows/win-x64/CppCliModuleTest.dll
  MainAppTest/bin/Debug/net10.0-windows/win-x64/MainAppTest_Test.dll
```

**Expected**: 1 DLL discovered (CppCliModuleTest excluded)

---

### TC-004: Wrong Configuration

**Setup**:
```text
TestSRC/
  Test/bin/Release/net10.0-windows/win-x64/Test_Test.dll
```

**Expected**: 0 DLLs discovered (Release config not matched)

---

### TC-005: Wrong TFM

**Setup**:
```text
TestSRC/
  Test/bin/Debug/net8.0/Test_Test.dll
```

**Expected**: 0 DLLs discovered (net8.0 TFM not matched)

---

### TC-006: Wrong RID

**Setup**:
```text
TestSRC/
  Test/bin/Debug/net10.0-windows/win-x86/Test_Test.dll
```

**Expected**: 0 DLLs discovered (win-x86 RID not matched)

---

### TC-007: Missing RID Directory

**Setup**:
```text
TestSRC/
  Test/bin/Debug/net10.0-windows/Test_Test.dll
```

**Expected**: 0 DLLs discovered (missing win-x64 subdirectory)

---

### TC-008: Wrong Filename Suffix

**Setup**:
```text
TestSRC/
  Test/bin/Debug/net10.0-windows/win-x64/Test.dll
  Test/bin/Debug/net10.0-windows/win-x64/TestHelper.dll
```

**Expected**: 0 DLLs discovered (no _Test.dll suffix)

---

### TC-009: Case Insensitivity

**Setup**:
```text
TestSRC/
  Test/bin/Debug/net10.0-windows/win-x64/MAINAPPTEST_TEST.DLL
```

**Expected**: 1 DLL discovered (case-insensitive match)

---

### TC-010: Empty TestSRC

**Setup**:
```text
TestSRC/ (empty directory)
```

**Expected**: 0 DLLs discovered, WARNING logged, exit code 0

---

### TC-011: TestSRC Missing

**Setup**: TestSRC directory doesn't exist

**Expected**: ERROR logged, exit code 1

---

## Integration with VSTest

### Command Construction

**Input**: Discovered DLL array:
```json
[
  "D:\\Repos\\App\\TestSRC\\Test1\\bin\\Debug\\net10.0-windows\\win-x64\\Test1_Test.dll",
  "D:\\Repos\\App\\TestSRC\\Test2\\bin\\Debug\\net10.0-windows\\win-x64\\Test2_Test.dll"
]
```

**Output**: vstest command:
```text
vstest.console.exe "D:\Repos\App\TestSRC\Test1\bin\Debug\net10.0-windows\win-x64\Test1_Test.dll" "D:\Repos\App\TestSRC\Test2\bin\Debug\net10.0-windows\win-x64\Test2_Test.dll" [user args]
```

**Notes**:
- Each path quoted (handles spaces)
- DLL paths placed **before** user vstest arguments
- Order of DLLs: undefined (vstest processes all)

---

## Future Enhancements (Not in V1)

### Configurable Patterns

**Idea**: Support `.testdiscovery.json` config file:
```json
{
  "root": "TestSRC",
  "patterns": [
    "*_Test.dll",
    "*Tests.dll"
  ],
  "pathFilter": "/bin/Debug/net10.0-windows.*/win-x64/",
  "exclude": [
    "CppCliModuleTest.dll",
    "LegacyTest.dll"
  ]
}
```

**Rationale**: Flexibility for non-standard project layouts

---

### Multiple Configurations

**Idea**: Support Release config discovery:
```bash
dotnet cake --target=Test --test-config=Release
```

**Rationale**: Allow testing Release builds (less common, but valid)

---

### Parallel Discovery

**Idea**: Use `Parallel.ForEach` for file enumeration

**Rationale**: Performance improvement for very large repos (1000+ test projects)

---

**Version**: 1.0  
**Status**: Specification complete and testable
