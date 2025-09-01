using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SaxonHECSharp;

public class SaxonHE : IDisposable
{
    private readonly string _saxonBinDir;

    public SaxonHE(string saxonBinDir = null)
    {
        if (string.IsNullOrWhiteSpace(saxonBinDir))
        {
            saxonBinDir = Path.Combine(AppContext.BaseDirectory,
                "runtimes",
                GetRuntimeIdentifier(),
                "native");
        }

        _saxonBinDir = saxonBinDir;
    }

    private string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        throw new PlatformNotSupportedException("Unsupported platform");
    }

    private string RunSaxonCommand(string exeName, string arguments)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindows)
            exeName += ".exe";

        string exePath = Path.Combine(_saxonBinDir, exeName);

        if (!File.Exists(exePath))
            throw new FileNotFoundException($"Executable not found: {exePath}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            if (process == null)
                throw new InvalidOperationException($"Failed to start process: {exePath}");

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"{exeName} failed:\n{error}\n{output}");
            }

            var result = output + error; // return both streams
            Console.WriteLine($"Run {exeName} - Args: {arguments}: {result}");
            return result;
        }
    }

    public string RunValidate(string xmlFile, string schemaFile)
    {
        return RunSaxonCommand("Validate",
            $"-s:\"{xmlFile}\" -xsd:\"{schemaFile}\"");
    }

    public bool Transform(string sourceXml, string stylesheet, string outputFile)
    {
        var result = RunSaxonCommand("Transform",
            $"-s:\"{sourceXml}\" -xsl:\"{stylesheet}\" -o:\"{outputFile}\"");

        return true;
    }

    public string RunQuery(string queryFile, string sourceXml, string outputFile)
    {
        return RunSaxonCommand("Query",
            $"-q:\"{queryFile}\" -s:\"{sourceXml}\" -o:\"{outputFile}\"");
    }

    public void Dispose()
    {

    }
}