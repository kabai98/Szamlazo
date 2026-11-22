using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Szamlazo.Infrastructure.Importing;

internal sealed class ExternalToolRunner(IConfiguration configuration, ILogger<ExternalToolRunner> logger)
{
    public async Task<string> RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = configuration[$"Tools:{executable}"] ?? executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
            start.ArgumentList.Add(arg);
        start.Environment["OMP_THREAD_LIMIT"] = "1";

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException();
        }
        catch (Win32Exception exception)
        {
            logger.LogError(exception, "Import tool {Tool} could not start", executable);
            throw new InvalidDataException($"A helyi feldolgozó eszköz nem indítható: {executable}. Ellenőrizd a telepítést és a Tools konfigurációt.", exception);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(token);
                await errors;
                if (process.ExitCode != 0)
                {
                    logger.LogWarning("Import tool {Tool} exited with code {ExitCode}", executable, process.ExitCode);
                    throw new InvalidDataException($"A feldolgozás sikertelen ({executable}). Ellenőrizd a fájlt és az OCR-eszköz beállításait.");
                }
                return await output;
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(output, errors);
            }
        }
    }

}
