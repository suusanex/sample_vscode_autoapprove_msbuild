# Tasks: Unified Build and Test Execution Script

**Feature**: 001-unified-build-script  
**Input**: Design documents from `/specs/001-unified-build-script/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: No test tasks included - tests not explicitly requested in feature specification. Focus is on build automation script implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and Cake environment setup

- [x] T001 Create build directory structure at repository root: build/
- [x] T002 Create .config directory and dotnet-tools.json manifest
- [x] T003 Add Cake.Tool to dotnet-tools.json (enables `dotnet tool restore`)
- [x] T004 Create build/build.cake with basic structure (empty targets)
- [x] T005 Verify Cake.Tool installation via `dotnet tool restore`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Implement argument parsing logic in build/build.cake (--target, --solution, --msbuild-path, --vstest-path, --test-src-path)
- [x] T007 Implement passthrough argument separation logic (double-dash separator parsing)
- [x] T008 [P] Create helper function FindRepositoryRoot() in build/build.cake (locates .git directory)
- [x] T009 [P] Create helper function ResolveAbsolutePath() in build/build.cake (resolves relative paths)
- [x] T010 Implement logging infrastructure (INFO/WARNING/ERROR prefixes, timestamp formatting)
- [x] T011 Implement error handling framework (exit code management, error message formatting)
- [x] T012 Create Default target in build/build.cake (alias for BuildAndTest)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Execute Build with Standard Options (Priority: P1) 🎯 MVP

**Goal**: Enable developers and CI to build solutions using a single command with automatic msbuild discovery and transparent argument passthrough.

**Independent Test**: 
```bash
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release
# Verify: msbuild executes, solution builds, correct exit code returned
```

### Implementation for User Story 1

- [x] T013 [P] [US1] Implement FindMSBuild() function in build/build.cake (checks MSBUILD_PATH env var, --msbuild-path argument)
- [x] T014 [P] [US1] Implement FindMSBuildViaVsWhere() function in build/build.cake (queries vswhere for MSBuild path)
- [x] T015 [P] [US1] Implement FindMSBuildViaPath() function in build/build.cake (searches PATH environment variable)
- [x] T016 [US1] Integrate MSBuild discovery chain in build/build.cake (explicit path → vswhere → PATH → error)
- [x] T017 [US1] Implement BuildMSBuildArguments() function in build/build.cake (solution path + passthrough args)
- [x] T018 [US1] Implement ExecuteMSBuild() function in build/build.cake (StartProcess with exit code capture)
- [x] T019 [US1] Create Build target in build/build.cake (orchestrates T016-T018)
- [x] T020 [US1] Add command echo logging before msbuild execution (log full command line)
- [x] T021 [US1] Add validation for --solution argument (required for Build target)
- [x] T022 [US1] Add validation for solution file existence (error if not found)

**Checkpoint**: At this point, User Story 1 should be fully functional - can build solutions with custom msbuild arguments

---

## Phase 4: User Story 2 - Execute Tests with Automatic DLL Discovery (Priority: P2)

**Goal**: Automatically discover and run test assemblies matching naming and path patterns, excluding specified DLLs.

**Independent Test**:
```bash
# Ensure test DLLs exist in TestSRC with pattern *_Test.dll in bin/Debug/net10.0-windows*/win-x64/
dotnet cake --target=Test
# Verify: test DLLs discovered, CppCliModuleTest.dll excluded, vstest executes all others
```

### Implementation for User Story 2

- [x] T023 [P] [US2] Implement FindVSTest() function in build/build.cake (checks VSTEST_PATH env var, --vstest-path argument)
- [x] T024 [P] [US2] Implement FindVSTestViaVsWhere() function in build/build.cake (queries vswhere for vstest.console.exe)
- [x] T025 [P] [US2] Implement FindVSTestViaCommonPaths() function in build/build.cake (checks VS 2022 Enterprise/Professional/Community/BuildTools paths)
- [x] T026 [P] [US2] Implement FindVSTestViaPath() function in build/build.cake (searches PATH environment variable)
- [x] T027 [US2] Integrate VSTest discovery chain in build/build.cake (explicit path → vswhere → common paths → PATH → error)
- [x] T028 [US2] Implement ResolveTestSrcRoot() function in build/build.cake (--test-src-path argument or default "TestSRC")
- [x] T029 [US2] Implement DiscoverTestDlls() function in build/build.cake (recursive enumeration, filename filter *_Test.dll)
- [x] T030 [US2] Add path pattern filter to DiscoverTestDlls() in build/build.cake (regex: /bin/Debug/net10\.0-windows[^/]*/win-x64/)
- [x] T031 [US2] Add exclusion filter to DiscoverTestDlls() in build/build.cake (exclude CppCliModuleTest.dll)
- [x] T032 [US2] Add discovery logging (log found DLLs, excluded DLLs, total scanned)
- [x] T033 [US2] Implement BuildVSTestArguments() function in build/build.cake (DLL paths + passthrough args)
- [x] T034 [US2] Implement ExecuteVSTest() function in build/build.cake (StartProcess with exit code capture)
- [x] T035 [US2] Create Test target in build/build.cake (orchestrates T027-T034)
- [x] T036 [US2] Add validation for TestSRC directory existence (error if not found)
- [x] T037 [US2] Handle no DLLs discovered case (WARNING log, skip execution, exit 0)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - can build OR test separately

---

## Phase 5: User Story 3 - Pass Custom Test Parameters (Priority: P3)

**Goal**: Enable developers and CI to customize test execution with vstest.console.exe options (filters, loggers, parallel execution).

**Independent Test**:
```bash
dotnet cake --target=Test -- /TestCaseFilter:"Priority=1" /Logger:trx /Parallel
# Verify: arguments passed to vstest.console.exe without modification, tests execute with filters
```

### Implementation for User Story 3

- [x] T038 [US3] Verify passthrough argument parsing works for Test target in build/build.cake (arguments after first --)
- [x] T039 [US3] Verify BuildVSTestArguments() preserves argument order in build/build.cake
- [x] T040 [US3] Add integration test scenario documentation to quickstart.md (test filters, loggers, parallel)
- [x] T041 [US3] Verify BuildAndTest target correctly splits msbuild/vstest arguments in build/build.cake (between first and second --)

**Checkpoint**: All user stories should now be independently functional - build with options, test with discovery, test with custom parameters

---

## Phase 6: BuildAndTest Integration

**Purpose**: Integrate Build and Test targets for sequential execution

- [x] T042 Create BuildAndTest target in build/build.cake (calls Build target, then Test target if build succeeds)
- [x] T043 Implement exit code logic for BuildAndTest (return msbuild exit code if fails, else vstest exit code)
- [x] T044 Add BuildAndTest passthrough argument parsing (msbuild args between first/second --, vstest args after second --)
- [x] T045 Test BuildAndTest with both tool arguments: `dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel`

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements, documentation, and CI integration

- [x] T046 [P] Add vswhere.exe availability check (warn if not found, document manual installation)
- [x] T047 [P] Update quickstart.md with actual command examples for all targets
- [x] T048 [P] Create example Azure Pipelines YAML in .azure-pipelines/build-and-test.yml
- [x] T049 [P] Create example GitHub Actions workflow in .github/workflows/build-and-test.yml
- [x] T050 Add prerequisite check to build.cake (verify .NET SDK version, Cake.Tool installed)
- [x] T051 Add help target to build.cake (--help displays usage information)
- [x] T052 Test error scenarios: msbuild not found, vstest not found, solution missing, TestSRC missing
- [x] T053 Test edge cases: no test DLLs found, invalid arguments, spaces in paths
- [x] T054 Create README.md in build/ directory with quick reference
- [x] T055 Run quickstart.md validation (verify all examples work)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001-T005) completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (T006-T012) completion
- **User Story 2 (Phase 4)**: Depends on Foundational (T006-T012) completion - Independent of US1
- **User Story 3 (Phase 5)**: Depends on User Story 2 (T023-T037) completion - Extends test execution
- **BuildAndTest (Phase 6)**: Depends on User Story 1 (T013-T022) AND User Story 2 (T023-T037) completion
- **Polish (Phase 7)**: Depends on all user stories and BuildAndTest integration

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Independent of US1 (can develop in parallel)
- **User Story 3 (P3)**: Depends on User Story 2 completion - Extends existing test execution

### Within Each User Story

**User Story 1 (Build)**:
1. Tool discovery functions (T013-T015) can run in parallel → independent functions
2. Discovery chain integration (T016) depends on T013-T015
3. Argument builder (T017) can be parallel with T016
4. Executor (T018) can be parallel with T016-T017
5. Target orchestration (T019) depends on T016-T018
6. Validation and logging (T020-T022) depend on T019

**User Story 2 (Test)**:
1. VSTest discovery functions (T023-T026) can run in parallel → independent functions
2. Discovery chain integration (T027) depends on T023-T026
3. TestSRC resolution (T028) independent, can be parallel
4. DLL discovery with filters (T029-T031) depends on T028
5. Discovery logging (T032) depends on T029-T031
6. Argument builder (T033) can be parallel with T029-T032
7. Executor (T034) can be parallel with T029-T033
8. Target orchestration (T035) depends on T027-T034
9. Validation and edge cases (T036-T037) depend on T035

**User Story 3 (Custom Parameters)**:
1. All tasks are verification/documentation - minimal dependencies
2. T038-T039 verify existing code works correctly
3. T040 updates documentation
4. T041 verifies BuildAndTest integration

### Parallel Opportunities

**Setup Phase**:
- T002-T004 can run in parallel (different files: .config/dotnet-tools.json, build/build.cake)

**Foundational Phase**:
- T008-T009 can run in parallel (independent helper functions)

**User Story 1**:
- T013, T014, T015 can run in parallel (independent MSBuild discovery functions)
- T017, T018 can run in parallel after T016 (independent builder and executor)

**User Story 2**:
- T023, T024, T025, T026 can run in parallel (independent VSTest discovery functions)
- T028 can run parallel with T023-T027 (independent TestSRC resolution)
- T033, T034 can run in parallel after T027-T032 (independent builder and executor)

**User Story 1 & User Story 2**:
- After Foundational phase complete, US1 and US2 can be worked on in parallel by different developers

**Polish Phase**:
- T046, T047, T048, T049, T054 can all run in parallel (different documentation files)

---

## Parallel Example: User Story 2 (Test Execution)

```bash
# Step 1: Launch all VSTest discovery functions in parallel
Task T023: "Implement FindVSTest() - checks env vars and args"
Task T024: "Implement FindVSTestViaVsWhere() - queries vswhere"
Task T025: "Implement FindVSTestViaCommonPaths() - checks known VS paths"
Task T026: "Implement FindVSTestViaPath() - searches PATH"

# Step 2: Integrate discovery chain (depends on Step 1)
Task T027: "Integrate VSTest discovery chain - try each method in order"

# Step 3: Launch test discovery and TestSRC resolution in parallel
Task T028: "Implement ResolveTestSrcRoot() - handle --test-src-path arg"
Task T029: "Implement DiscoverTestDlls() - recursive enumeration"

# Step 4: Add filters sequentially (depend on T029)
Task T030: "Add path pattern filter to DiscoverTestDlls()"
Task T031: "Add exclusion filter to DiscoverTestDlls()"
Task T032: "Add discovery logging"

# Step 5: Launch argument builder and executor in parallel (depend on previous)
Task T033: "Implement BuildVSTestArguments() - DLL paths + passthrough"
Task T034: "Implement ExecuteVSTest() - process execution"

# Step 6: Orchestrate (depends on all above)
Task T035: "Create Test target - orchestrates all logic"

# Step 7: Validation (depends on T035)
Task T036: "Add TestSRC validation"
Task T037: "Handle no DLLs case"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005) - ~30 minutes
2. Complete Phase 2: Foundational (T006-T012) - ~2 hours
3. Complete Phase 3: User Story 1 (T013-T022) - ~4 hours
4. **STOP and VALIDATE**: Test building solutions with various msbuild arguments
5. **MVP READY**: Can now build projects from single entry point

**Total MVP Time Estimate**: ~6-7 hours

### Incremental Delivery

1. **Foundation** (Phase 1-2): Setup + Core infrastructure → ~2.5 hours
2. **MVP** (Phase 3): User Story 1 → Build capability → ~4 hours → **Deploy/Demo**
3. **Testing** (Phase 4): User Story 2 → Test discovery and execution → ~5 hours → **Deploy/Demo**
4. **Advanced** (Phase 5): User Story 3 → Custom test parameters → ~2 hours → **Deploy/Demo**
5. **Integration** (Phase 6): BuildAndTest → Combined workflow → ~1 hour → **Deploy/Demo**
6. **Polish** (Phase 7): Documentation, CI examples, edge cases → ~3 hours → **Final Release**

**Total Time Estimate**: ~17-18 hours

### Parallel Team Strategy

With two developers:

1. **Together**: Complete Setup + Foundational (Phase 1-2) → ~2.5 hours
2. **Split**:
   - **Developer A**: User Story 1 (Build) → Phase 3 → ~4 hours
   - **Developer B**: User Story 2 (Test) → Phase 4 → ~5 hours
3. **Developer A**: User Story 3 (Custom params) → Phase 5 → ~2 hours
4. **Together**: BuildAndTest Integration (Phase 6) → ~1 hour
5. **Split parallel**:
   - **Developer A**: CI examples (T048-T049)
   - **Developer B**: Documentation (T047, T054)
6. **Together**: Testing and validation (Phase 7) → ~2 hours

**Total Parallel Time Estimate**: ~10-12 hours (wall clock time)

---

## Validation Checkpoints

### After Phase 3 (User Story 1)
```bash
# Test 1: Basic build
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx
# Expected: Solution builds successfully

# Test 2: Build with configuration
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release
# Expected: Release build succeeds

# Test 3: Build with parallel
dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /m:4
# Expected: Parallel build succeeds

# Test 4: Missing solution
dotnet cake --target=Build
# Expected: ERROR - --solution argument required

# Test 5: Invalid solution path
dotnet cake --target=Build --solution=NotExist.sln
# Expected: ERROR - Solution file not found
```

### After Phase 4 (User Story 2)
```bash
# Test 1: Basic test execution
dotnet cake --target=Test
# Expected: Test DLLs discovered and executed

# Test 2: Verify exclusion
# Check logs for: "Excluded 1 assembly: CppCliModuleTest.dll"

# Test 3: Custom TestSRC path
dotnet cake --target=Test --test-src-path=tests
# Expected: Discovers DLLs in alternate path

# Test 4: No DLLs found (empty TestSRC)
# Expected: WARNING logged, exit code 0

# Test 5: TestSRC missing
mv TestSRC TestSRC.bak
dotnet cake --target=Test
# Expected: ERROR - TestSRC directory not found
```

### After Phase 5 (User Story 3)
```bash
# Test 1: Test with filter
dotnet cake --target=Test -- /TestCaseFilter:"Priority=1"
# Expected: Only Priority=1 tests execute

# Test 2: Test with logger
dotnet cake --target=Test -- /Logger:trx
# Expected: .trx file generated

# Test 3: Parallel test execution
dotnet cake --target=Test -- /Parallel
# Expected: Tests run in parallel
```

### After Phase 6 (BuildAndTest)
```bash
# Test 1: Build and test together
dotnet cake --solution=SRC/Apps/Apps.slnx
# Expected: Build succeeds, then tests execute

# Test 2: BuildAndTest with arguments for both
dotnet cake --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel
# Expected: Release build, parallel tests

# Test 3: Build fails
# Introduce build error in code
dotnet cake --solution=SRC/Apps/Apps.slnx
# Expected: Non-zero exit code, tests not executed
```

---

## Notes

- All tasks follow checklist format: `- [ ] [TaskID] [P?] [Story?] Description`
- [P] tasks can run in parallel (different files/functions, no dependencies)
- [Story] labels (US1, US2, US3) map tasks to user stories for traceability
- Each user story is independently completable and testable
- No test tasks included - not explicitly requested in feature specification
- Focus on build automation script implementation only
- Commit after each task or logical group of tasks
- Stop at any checkpoint to validate story works independently
- TestSRC directory structure and existing test DLLs are pre-existing (not created by these tasks)

---

## Task Count Summary

- **Setup**: 5 tasks
- **Foundational**: 7 tasks (BLOCKING)
- **User Story 1 (Build)**: 10 tasks
- **User Story 2 (Test)**: 15 tasks
- **User Story 3 (Custom params)**: 4 tasks
- **BuildAndTest Integration**: 4 tasks
- **Polish**: 10 tasks

**Total**: 55 tasks

**Parallel opportunities**: 15 tasks marked [P]

**MVP (US1 only)**: 22 tasks (Setup + Foundational + US1)

**Full feature**: 55 tasks
