using System.Diagnostics;

var arguments = args.ToList();
if (arguments.Count == 0 || arguments.Contains("--help") || arguments.Contains("-h"))
{
    PrintHelp();
    return 0;
}

if (arguments.Contains("--version") || arguments.Contains("-v"))
{
    Console.WriteLine("1.3.0");
    return 0;
}

var repoRoot = FindRepoRoot(Environment.CurrentDirectory);
var moduleRoot = FindModuleRoot(repoRoot) ?? InstallFormatterModule();
if (moduleRoot is null)
{
    Console.Error.WriteLine(
        "Unable to locate the CSharpier formatter runtime. Run `npm ci` in tools/csharpier or allow npm to download @dprint/csharpier@0.2.0."
    );
    return 1;
}

var scriptPath = Path.Combine(AppContext.BaseDirectory, "csharpier.mjs");
if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"Missing formatter script at {scriptPath}.");
    return 1;
}

var startInfo = new ProcessStartInfo { FileName = "node", UseShellExecute = false };
startInfo.ArgumentList.Add(scriptPath);
startInfo.ArgumentList.Add("--repo-root");
startInfo.ArgumentList.Add(repoRoot);
startInfo.ArgumentList.Add("--module-root");
startInfo.ArgumentList.Add(moduleRoot);
foreach (var argument in arguments)
{
    startInfo.ArgumentList.Add(argument);
}

using var process = Process.Start(startInfo);
if (process is null)
{
    Console.Error.WriteLine("Unable to start node.");
    return 1;
}

process.WaitForExit();
return process.ExitCode;

static void PrintHelp()
{
    Console.WriteLine(
        """
        csharpier 1.3.0

        Usage:
          csharpier check [path ...]
          csharpier format [path ...]

        Options:
          --include-generated   Include files marked as generated.
          --version             Print the CSharpier version.
          --help                Print this help.
        """
    );
}

static string FindRepoRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (
            File.Exists(Path.Combine(directory.FullName, ".config", "dotnet-tools.json"))
            || Directory.Exists(Path.Combine(directory.FullName, ".git"))
        )
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return start;
}

static string? FindModuleRoot(string repoRoot)
{
    var candidates = new List<string>
    {
        Path.Combine(repoRoot, "tools", "csharpier"),
        Path.Combine(repoRoot, "src", "Anthology", "ClientApp"),
        GetCacheDirectory(),
    };

    foreach (var candidate in candidates)
    {
        if (
            File.Exists(
                Path.Combine(candidate, "node_modules", "@dprint", "csharpier", "package.json")
            )
        )
        {
            return candidate;
        }
    }

    return null;
}

static string? InstallFormatterModule()
{
    var cacheDirectory = GetCacheDirectory();
    Directory.CreateDirectory(cacheDirectory);
    var packageJson = Path.Combine(cacheDirectory, "package.json");
    if (!File.Exists(packageJson))
    {
        File.WriteAllText(packageJson, """{"name":"csharpier-tool","private":true}""");
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = "npm",
        UseShellExecute = false,
        WorkingDirectory = cacheDirectory,
    };
    startInfo.ArgumentList.Add("install");
    startInfo.ArgumentList.Add("@dprint/csharpier@0.2.0");
    startInfo.ArgumentList.Add("@dprint/formatter@0.6.0");
    startInfo.ArgumentList.Add("--no-audit");
    startInfo.ArgumentList.Add("--no-fund");
    startInfo.ArgumentList.Add("--ignore-scripts");

    using var process = Process.Start(startInfo);
    process?.WaitForExit();
    if (process?.ExitCode != 0)
    {
        return null;
    }

    return File.Exists(
        Path.Combine(cacheDirectory, "node_modules", "@dprint", "csharpier", "package.json")
    )
        ? cacheDirectory
        : null;
}

static string GetCacheDirectory()
{
    var cacheRoot =
        Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
    return Path.Combine(cacheRoot, "csharpier-tool");
}
