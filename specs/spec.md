# Feature Specification: Unified Build and Test Execution Script

**Feature Branch**: `001-unified-build-script`  
**Created**: December 16, 2025  
**Status**: Draft  
**Input**: User description: "このリポジトリに、ローカル実行と CI（例: Azure Pipelines）で共通利用でき、かつ GitHub Copilot のターミナル実行承認を安定させるための「単一入口のビルド/テスト実行スクリプト」を導入する。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute Build with Standard Options (Priority: P1)

A developer or CI system needs to build the solution using standard build configurations without worrying about locating msbuild or managing tool paths.

**Why this priority**: This is the foundational capability - without reliable builds, no other development activities can proceed. It establishes the single entry point pattern that the entire feature depends on.

**Independent Test**: Can be fully tested by invoking the script with a solution file path and verifying that msbuild executes successfully with the provided arguments.

**Acceptance Scenarios**:

1. **Given** a developer is working on their local machine, **When** they execute the build script with a solution file and configuration parameters, **Then** the script locates msbuild and executes the build with the specified options
2. **Given** a CI pipeline needs to build the solution, **When** it invokes the build script with the same parameters used locally, **Then** the build executes identically to the local environment
3. **Given** a developer wants to pass custom msbuild flags, **When** they provide additional arguments to the build script, **Then** all arguments are transparently forwarded to msbuild without modification

---

### User Story 2 - Execute Tests with Automatic DLL Discovery (Priority: P2)

A developer or CI system needs to run all eligible test assemblies without manually specifying each DLL path, while ensuring excluded assemblies are never executed.

**Why this priority**: Automated testing is critical for quality assurance but depends on the build capability from P1. The automatic discovery reduces maintenance burden as tests are added or moved.

**Independent Test**: Can be fully tested by placing test DLLs matching the search criteria in the expected locations, executing the test script, and verifying that only eligible DLLs are tested and CppCliModuleTest.dll is excluded.

**Acceptance Scenarios**:

1. **Given** test DLLs exist under TestSRC in the specified path pattern, **When** the test script is executed, **Then** it discovers all DLLs matching "*_Test.dll" in "\bin\Debug\net10.0-windows.*\win-x64\" paths
2. **Given** CppCliModuleTest.dll exists in the search path, **When** the test script discovers test DLLs, **Then** CppCliModuleTest.dll is excluded from the test execution list
3. **Given** the test script has discovered eligible DLLs, **When** it invokes vstest.console.exe, **Then** all discovered DLLs (except excluded ones) are passed to the test runner

---

### User Story 3 - Pass Custom Test Parameters (Priority: P3)

A developer or CI system needs to customize test execution with specific vstest.console.exe options like test filters, logger configurations, or parallel execution settings.

**Why this priority**: While basic test execution (P2) covers most scenarios, advanced users need control over test execution behavior for debugging, filtering, or performance optimization.

**Independent Test**: Can be fully tested by executing the test script with various vstest.console.exe arguments (e.g., test filters, logger settings) and verifying these are passed through without modification.

**Acceptance Scenarios**:

1. **Given** a developer wants to run specific test categories, **When** they provide test filter arguments to the test script, **Then** the arguments are transparently forwarded to vstest.console.exe
2. **Given** a CI pipeline needs custom logging, **When** it invokes the test script with logger parameters, **Then** vstest.console.exe receives and applies those logger settings

---

### Edge Cases

- What happens when msbuild or vstest.console.exe cannot be located in standard paths?
- How does the script handle scenarios where no test DLLs match the search criteria?
- What happens when the TestSRC directory does not exist?
- How does the script behave when provided with invalid or conflicting arguments?
- What happens if a test DLL is locked or inaccessible at runtime?

## Requirements *(mandatory)*

### Functional Requirements

**Build Functionality**:

- **FR-001**: Script MUST accept a solution or project file path as input for build operations
- **FR-002**: Script MUST locate msbuild executable on the system (checking standard installation paths like Visual Studio installations)
- **FR-003**: Script MUST accept arbitrary msbuild command-line arguments and pass them to msbuild without modification or interpretation
- **FR-004**: Script MUST execute msbuild with the provided file path and all forwarded arguments
- **FR-005**: Script MUST return the same exit code that msbuild returns

**Test Functionality**:

- **FR-006**: Script MUST recursively search the "TestSRC" directory for test assemblies
- **FR-007**: Script MUST filter discovered files to only include those with names ending in "_Test.dll"
- **FR-008**: Script MUST further filter to only include DLLs where the full path matches the pattern "\bin\Debug\net10.0-windows.*\win-x64\"
- **FR-009**: Script MUST exclude "CppCliModuleTest.dll" from test execution regardless of whether it matches other criteria
- **FR-010**: Script MUST locate vstest.console.exe executable on the system (checking standard installation paths like Visual Studio installations)
- **FR-011**: Script MUST accept arbitrary vstest.console.exe command-line arguments and pass them to the test runner without modification
- **FR-012**: Script MUST execute vstest.console.exe with all discovered DLL paths (after filtering and exclusions) plus all forwarded arguments
- **FR-013**: Script MUST return the same exit code that vstest.console.exe returns

**General Requirements**:

- **FR-014**: Script MUST work identically when executed from a local development machine or CI environment
- **FR-015**: Script MUST provide clear error messages when required tools (msbuild/vstest.console.exe) cannot be found
- **FR-016**: Script MUST provide clear error messages when required inputs (like file paths) are missing or invalid
- **FR-017**: Script responsibility MUST be limited to tool location, file discovery, and command execution - no build process customization or test orchestration logic

### Assumptions

- Visual Studio or Build Tools are installed on the system where the script runs
- msbuild and vstest.console.exe are in standard installation locations or accessible via PATH
- Test assemblies follow the existing naming convention (*_Test.dll) and output to the specified path pattern
- PowerShell execution policy allows script execution
- The repository maintains the current TestSRC directory structure

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developers can execute builds and tests using the same script invocation command on both local machines and CI systems
- **SC-002**: Terminal approval rules can be defined for a single script path instead of multiple msbuild/vstest command variations
- **SC-003**: Build and test execution commands remain stable even as project configurations change (no command-line drift)
- **SC-004**: Test execution automatically includes new test assemblies that match the naming and path conventions without manual script updates
- **SC-005**: CppCliModuleTest.dll is never executed during test runs, verified by test execution logs
