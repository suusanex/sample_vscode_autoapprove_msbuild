//////////////////////////////////////////////////////////////////////
// Unified Build and Test Execution Script
//
// Entry point:
//   dotnet tool restore
//   dotnet cake build/build.cake --solution=SRC/Apps/Apps.slnx
//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

string Timestamp() => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

void LogInfo(string message) => Console.WriteLine($"[INFO] {Timestamp()} | {message}");
void LogWarning(string message) => Console.WriteLine($"[WARNING] {Timestamp()} | {message}");
void LogError(string message) => Console.Error.WriteLine($"[ERROR] {Timestamp()} | {message}");

void Fail(string message, int exitCode = 1)
{
    LogError(message);
    System.Environment.Exit(exitCode);
}

bool prereqsChecked = false;
bool warnedVsWhereMissing = false;

bool FileExistsAt(string path) => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);

string QuoteIfNeeded(string value)
{
    if (string.IsNullOrEmpty(value))
    {
        return value;
    }

    return value.Contains(' ') ? $"\"{value}\"" : value;
}

string TryGetVsWherePath()
{
    var attempted = new List<string>();
    var programFilesX86 = System.Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(programFilesX86))
    {
        var candidate = System.IO.Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        attempted.Add(candidate);
        if (FileExistsAt(candidate))
        {
            return candidate;
        }
    }

    // Fallback: try PATH
    var output = new List<string>();
    var exitCode = StartProcess("where.exe", new ProcessSettings {
        Arguments = new ProcessArgumentBuilder().Append("vswhere.exe"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) output.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });

    if (exitCode == 0)
    {
        var first = output.FirstOrDefault(l => l.EndsWith("vswhere.exe", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(first) && FileExistsAt(first))
        {
            return first;
        }
    }

    return string.Empty;
}

string FindMSBuild(string msbuildPathFromArg)
{
    var explicitFromArg = (msbuildPathFromArg ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(explicitFromArg))
    {
        var resolved = ResolveAbsolutePath(explicitFromArg, cwdFullPath);
        if (!FileExistsAt(resolved))
        {
            Fail($"Specified msbuild path not found: {resolved}");
        }
        return resolved;
    }

    var fromEnv = (System.Environment.GetEnvironmentVariable("MSBUILD_PATH") ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(fromEnv))
    {
        var resolved = ResolveAbsolutePath(fromEnv, cwdFullPath);
        if (!FileExistsAt(resolved))
        {
            Fail($"MSBUILD_PATH is set but not found: {resolved}");
        }
        return resolved;
    }

    var viaVsWhere = FindMSBuildViaVsWhere();
    if (!string.IsNullOrWhiteSpace(viaVsWhere))
    {
        return viaVsWhere;
    }

    var viaPath = FindMSBuildViaPath();
    if (!string.IsNullOrWhiteSpace(viaPath))
    {
        return viaPath;
    }

    return string.Empty;
}

string FindMSBuildViaVsWhere()
{
    var vswhere = TryGetVsWherePath();
    if (string.IsNullOrWhiteSpace(vswhere))
    {
        if (!warnedVsWhereMissing)
        {
            warnedVsWhereMissing = true;
            LogWarning("vswhere.exe not found. MSBuild/VSTest discovery will fall back to common paths/PATH. Install Visual Studio (recommended) or vswhere from https://github.com/microsoft/vswhere.");
        }
        return string.Empty;
    }

    var output = new List<string>();
    var args = new ProcessArgumentBuilder()
        .Append("-latest")
        .Append("-products")
        .Append("*")
        .Append("-requires")
        .Append("Microsoft.Component.MSBuild")
        .Append("-find")
        .Append("MSBuild\\**\\Bin\\MSBuild.exe");

    var exitCode = StartProcess(vswhere, new ProcessSettings {
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) output.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });

    if (exitCode != 0)
    {
        return string.Empty;
    }

    var candidate = output.FirstOrDefault(l => l.EndsWith("MSBuild.exe", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(candidate) && FileExistsAt(candidate))
    {
        return candidate;
    }

    return string.Empty;
}

string FindMSBuildViaPath()
{
    var output = new List<string>();
    var exitCode = StartProcess("where.exe", new ProcessSettings {
        Arguments = new ProcessArgumentBuilder().Append("msbuild"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) output.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });

    if (exitCode != 0)
    {
        return string.Empty;
    }

    var candidate = output.FirstOrDefault(l => l.EndsWith("msbuild.exe", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(candidate) && FileExistsAt(candidate))
    {
        return candidate;
    }

    return string.Empty;
}

ProcessArgumentBuilder BuildMSBuildArguments(string solutionPath, IReadOnlyList<string> passthroughArgs)
{
    var args = new ProcessArgumentBuilder();
    args.AppendQuoted(solutionPath);
    foreach (var arg in passthroughArgs)
    {
        args.Append(arg);
    }
    return args;
}

int ExecuteMSBuild(string msbuildPath, ProcessArgumentBuilder arguments, DirectoryPath workingDirectory)
{
    return StartProcess(msbuildPath, new ProcessSettings {
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = false,
        RedirectStandardError = false
    });
}

string FindVSTest(string vstestPathFromArg)
{
    var explicitFromArg = (vstestPathFromArg ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(explicitFromArg))
    {
        var resolved = ResolveAbsolutePath(explicitFromArg, cwdFullPath);
        if (!FileExistsAt(resolved))
        {
            Fail($"Specified vstest path not found: {resolved}");
        }
        return resolved;
    }

    var fromEnv = (System.Environment.GetEnvironmentVariable("VSTEST_PATH") ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(fromEnv))
    {
        var resolved = ResolveAbsolutePath(fromEnv, cwdFullPath);
        if (!FileExistsAt(resolved))
        {
            Fail($"VSTEST_PATH is set but not found: {resolved}");
        }
        return resolved;
    }

    var viaVsWhere = FindVSTestViaVsWhere();
    if (!string.IsNullOrWhiteSpace(viaVsWhere))
    {
        return viaVsWhere;
    }

    var viaCommon = FindVSTestViaCommonPaths();
    if (!string.IsNullOrWhiteSpace(viaCommon))
    {
        return viaCommon;
    }

    var viaPath = FindVSTestViaPath();
    if (!string.IsNullOrWhiteSpace(viaPath))
    {
        return viaPath;
    }

    return string.Empty;
}

string FindVSTestViaVsWhere()
{
    var vswhere = TryGetVsWherePath();
    if (string.IsNullOrWhiteSpace(vswhere))
    {
        if (!warnedVsWhereMissing)
        {
            warnedVsWhereMissing = true;
            LogWarning("vswhere.exe not found. MSBuild/VSTest discovery will fall back to common paths/PATH. Install Visual Studio (recommended) or vswhere from https://github.com/microsoft/vswhere.");
        }
        return string.Empty;
    }

    var output = new List<string>();
    var args = new ProcessArgumentBuilder()
        .Append("-latest")
        .Append("-products")
        .Append("*")
        .Append("-requires")
        .Append("Microsoft.VisualStudio.Component.TestTools")
        .Append("-find")
        .Append("**\\vstest.console.exe");

    var exitCode = StartProcess(vswhere, new ProcessSettings {
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) output.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });

    if (exitCode != 0)
    {
        return string.Empty;
    }

    var candidate = output.FirstOrDefault(l => l.EndsWith("vstest.console.exe", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(candidate) && FileExistsAt(candidate))
    {
        return candidate;
    }

    return string.Empty;
}

string FindVSTestViaCommonPaths()
{
    var programFiles = System.Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty;
    if (string.IsNullOrWhiteSpace(programFiles))
    {
        return string.Empty;
    }

    var editions = new[] { "Enterprise", "Professional", "Community", "BuildTools" };
    foreach (var edition in editions)
    {
        var candidate = System.IO.Path.Combine(programFiles, "Microsoft Visual Studio", "2022", edition, "Common7", "IDE", "Extensions", "TestPlatform", "vstest.console.exe");
        if (FileExistsAt(candidate))
        {
            return candidate;
        }
    }

    return string.Empty;
}

string FindVSTestViaPath()
{
    var output = new List<string>();
    var exitCode = StartProcess("where.exe", new ProcessSettings {
        Arguments = new ProcessArgumentBuilder().Append("vstest.console.exe"),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) output.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });

    if (exitCode != 0)
    {
        return string.Empty;
    }

    var candidate = output.FirstOrDefault(l => l.EndsWith("vstest.console.exe", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(candidate) && FileExistsAt(candidate))
    {
        return candidate;
    }

    return string.Empty;
}

string ResolveTestSrcRoot(string testSrcPathFromArg)
{
    var raw = (testSrcPathFromArg ?? string.Empty).Trim();
    var root = string.IsNullOrWhiteSpace(raw) ? "TestSRC" : raw;
    return ResolveAbsolutePath(root, repoRootFullPath);
}

(List<string> discovered, List<string> excluded, int totalScanned) DiscoverTestDlls(string testSrcRoot)
{
    var discovered = new List<string>();
    var excluded = new List<string>();
    var totalScanned = 0;

    var pathPattern = new Regex(@"/bin/Debug/net10\.0-windows[^/]*/win-x64/", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    foreach (var file in System.IO.Directory.EnumerateFiles(testSrcRoot, "*", SearchOption.AllDirectories))
    {
        totalScanned++;
        var fileName = System.IO.Path.GetFileName(file);
        if (string.Equals(fileName, "CppCliModuleTest.dll", StringComparison.OrdinalIgnoreCase))
        {
            excluded.Add(file);
            continue;
        }

        if (!fileName.EndsWith("_Test.dll", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var normalized = file.Replace('\\', '/');
        if (!pathPattern.IsMatch(normalized))
        {
            continue;
        }

        discovered.Add(System.IO.Path.GetFullPath(file));
    }

    return (discovered, excluded, totalScanned);
}

ProcessArgumentBuilder BuildVSTestArguments(IReadOnlyList<string> testAssemblies, IReadOnlyList<string> passthroughArgs)
{
    var args = new ProcessArgumentBuilder();
    foreach (var assemblyPath in testAssemblies)
    {
        args.AppendQuoted(assemblyPath);
    }
    foreach (var arg in passthroughArgs)
    {
        args.Append(arg);
    }
    return args;
}

int ExecuteVSTest(string vstestPath, ProcessArgumentBuilder arguments, DirectoryPath workingDirectory)
{
    return StartProcess(vstestPath, new ProcessSettings {
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = false,
        RedirectStandardError = false
    });
}

(int exitCode, List<string> stdoutLines) RunProcessCaptureLines(string fileName, ProcessArgumentBuilder args)
{
    var lines = new List<string>();
    var exitCode = StartProcess(fileName, new ProcessSettings {
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Silent = true,
        RedirectedStandardOutputHandler = line => { if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim()); return line; },
        RedirectedStandardErrorHandler = line => { return line; }
    });
    return (exitCode, lines);
}

void CheckPrerequisites()
{
    if (prereqsChecked)
    {
        return;
    }
    prereqsChecked = true;

    var (dotnetExit, dotnetOut) = RunProcessCaptureLines("dotnet", new ProcessArgumentBuilder().Append("--version"));
    if (dotnetExit != 0 || dotnetOut.Count == 0)
    {
        Fail(".NET SDK not available. Install .NET SDK 8.0+ and ensure `dotnet` is on PATH.");
    }
    LogInfo($".NET SDK: {dotnetOut[0]}");

    // If local tool manifest exists, ensure Cake.Tool is listed (team/CI scenario).
    var toolManifestPath = System.IO.Path.Combine(repoRootFullPath, ".config", "dotnet-tools.json");
    if (FileExistsAt(toolManifestPath))
    {
        var (toolExit, toolList) = RunProcessCaptureLines("dotnet", new ProcessArgumentBuilder().Append("tool").Append("list").Append("--local"));
        if (toolExit == 0)
        {
            var hasCake = toolList.Any(l => l.StartsWith("cake.tool", StringComparison.OrdinalIgnoreCase));
            if (!hasCake)
            {
                Fail("Cake.Tool is not installed in the local tool manifest. Run: dotnet tool restore");
            }
        }
    }
}

int RunBuild(IReadOnlyList<string> passthroughArgs)
{
    CheckPrerequisites();

    if (string.IsNullOrWhiteSpace(solutionArg))
    {
        Fail("Build target requires --solution argument.\nUsage: dotnet cake --target=Build --solution=<path>");
    }

    var solutionPath = ResolveAbsolutePath(solutionArg, cwdFullPath);
    if (!FileExistsAt(solutionPath))
    {
        Fail($"Solution file not found: {solutionPath}");
    }

    var msbuildPath = FindMSBuild(msbuildPathArg);
    if (string.IsNullOrWhiteSpace(msbuildPath))
    {
        Fail("msbuild not found. Install Visual Studio Build Tools or set MSBUILD_PATH environment variable.");
    }

    LogInfo($"Resolved msbuild: {msbuildPath}");

    var msbuildArguments = BuildMSBuildArguments(solutionPath, passthroughArgs);
    var echo = $"{QuoteIfNeeded(msbuildPath)} {QuoteIfNeeded(solutionPath)}";
    if (passthroughArgs.Count > 0)
    {
        echo += " " + string.Join(" ", passthroughArgs.Select(QuoteIfNeeded));
    }
    LogInfo($"Executing: {echo}");

    return ExecuteMSBuild(msbuildPath, msbuildArguments, repoRoot);
}

int RunTest(IReadOnlyList<string> passthroughArgs)
{
    CheckPrerequisites();

    var vstestPath = FindVSTest(vstestPathArg);
    if (string.IsNullOrWhiteSpace(vstestPath))
    {
        Fail("vstest.console.exe not found. Install Visual Studio Test Platform or set VSTEST_PATH.");
    }

    LogInfo($"Resolved vstest: {vstestPath}");

    var testSrcRoot = ResolveTestSrcRoot(testSrcPathArg);
    if (!System.IO.Directory.Exists(testSrcRoot))
    {
        Fail($"TestSRC directory not found at: {testSrcRoot}");
    }

    LogInfo($"Discovering test DLLs in: {testSrcRoot}");
    var (dlls, excluded, totalScanned) = DiscoverTestDlls(testSrcRoot);

    if (excluded.Count > 0)
    {
        LogInfo($"Excluded {excluded.Count} assembly:");
        foreach (var path in excluded)
        {
            LogInfo($"  - {System.IO.Path.GetFileName(path)} (full path: {path})");
        }
    }

    if (dlls.Count == 0)
    {
        LogWarning("No test assemblies found matching discovery criteria. Skipping test execution.");
        if (passthroughArgs.Count > 0)
        {
            LogWarning($"Provided vstest args (not executed due to no DLLs): {string.Join(" ", passthroughArgs.Select(QuoteIfNeeded))}");
        }
        LogWarning($"Search criteria:\n  - Root: {testSrcRoot}\n  - Pattern: *_Test.dll in /bin/Debug/net10.0-windows.*/win-x64/\n  - Excluded: CppCliModuleTest.dll\nTotal files scanned: {totalScanned}");
        return 0;
    }

    LogInfo($"Found {dlls.Count} test assemblies:");
    foreach (var dll in dlls)
    {
        LogInfo($"  - {dll}");
    }

    var vstestArguments = BuildVSTestArguments(dlls, passthroughArgs);
    var echo = $"{QuoteIfNeeded(vstestPath)}";
    echo += " " + string.Join(" ", dlls.Select(QuoteIfNeeded));
    if (passthroughArgs.Count > 0)
    {
        echo += " " + string.Join(" ", passthroughArgs.Select(QuoteIfNeeded));
    }
    LogInfo($"Executing: {echo}");

    return ExecuteVSTest(vstestPath, vstestArguments, repoRoot);
}

int RunBuildAndTest(IReadOnlyList<string> msbuildArgs, IReadOnlyList<string> vstestArgs)
{
    var buildExit = RunBuild(msbuildArgs);
    if (buildExit != 0)
    {
        return buildExit;
    }
    return RunTest(vstestArgs);
}

DirectoryPath FindRepositoryRoot()
{
    var current = new System.IO.DirectoryInfo(MakeAbsolute(Directory("./")).FullPath);
    var probe = current;
    while (probe != null)
    {
        var gitDir = System.IO.Path.Combine(probe.FullName, ".git");
        if (System.IO.Directory.Exists(gitDir))
        {
            return new DirectoryPath(probe.FullName);
        }
        probe = probe.Parent;
    }

    Fail("Repository root not found (could not locate .git in parent directories).");
    return null;
}

string ResolveAbsolutePath(string path, string baseDirectory)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return path;
    }

    if (System.IO.Path.IsPathRooted(path))
    {
        return System.IO.Path.GetFullPath(path);
    }

    return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, path));
}

IReadOnlyList<string> GetPassthroughTokens()
{
    // Passthrough arguments are everything after the first standalone `--` separator.
    // This intentionally keeps tokens verbatim (including '-' prefixed args) so we don't
    // accidentally drop valid msbuild/vstest options.
    var args = System.Environment.GetCommandLineArgs().Skip(1).ToList();
    var firstSeparatorIndex = args.FindIndex(a => a == "--");
    if (firstSeparatorIndex < 0)
    {
        return Array.Empty<string>();
    }

    return args.Skip(firstSeparatorIndex + 1).ToList();
}

(IReadOnlyList<string> msbuildArgs, IReadOnlyList<string> vstestArgs) ParsePassthrough(string normalizedTarget)
{
    var tokens = GetPassthroughTokens();

    if (string.Equals(normalizedTarget, "Test", StringComparison.OrdinalIgnoreCase))
    {
        // For Test, everything (except separators) is vstest args.
        return (Array.Empty<string>(), tokens.Where(t => t != "--").ToList());
    }

    var splitIndex = tokens.ToList().FindIndex(t => t == "--");
    if (splitIndex < 0)
    {
        return (tokens.Where(t => t != "--").ToList(), Array.Empty<string>());
    }

    var msbuild = tokens.Take(splitIndex).Where(t => t != "--").ToList();
    var vstest = tokens.Skip(splitIndex + 1).Where(t => t != "--").ToList();
    return (msbuild, vstest);
}

var target = Argument("target", "Default");
var solutionArg = Argument("solution", string.Empty);
var msbuildPathArg = Argument("msbuild-path", string.Empty);
var vstestPathArg = Argument("vstest-path", string.Empty);
var testSrcPathArg = Argument("test-src-path", string.Empty);

var repoRoot = FindRepositoryRoot();
var repoRootFullPath = repoRoot.FullPath;
var cwdFullPath = repoRootFullPath;

var effectiveTargetForParsing = string.Equals(target, "Default", StringComparison.OrdinalIgnoreCase)
    ? "BuildAndTest"
    : target;

var (msbuildPassthroughArgs, vstestPassthroughArgs) = ParsePassthrough(effectiveTargetForParsing);

Task("Help")
    .Does(() => {
        LogInfo("Usage:");
        LogInfo("  dotnet tool restore");
        LogInfo("  dotnet cake --target=Build --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release");
        LogInfo("  dotnet cake --target=Test -- /Parallel /Logger:trx");
        LogInfo("  dotnet cake --target=BuildAndTest --solution=SRC/Apps/Apps.slnx -- /p:Configuration=Release -- /Parallel");
    });

Task("Build")
    .Does(() => {
        var exitCode = RunBuild(msbuildPassthroughArgs);
        System.Environment.Exit(exitCode);
    });

Task("Test")
    .Does(() => {
        var exitCode = RunTest(vstestPassthroughArgs);
        System.Environment.Exit(exitCode);
    });

Task("BuildAndTest")
    .Does(() => {
        var exitCode = RunBuildAndTest(msbuildPassthroughArgs, vstestPassthroughArgs);
        System.Environment.Exit(exitCode);
    });

Task("Default")
    .IsDependentOn("BuildAndTest");

RunTarget(target);
