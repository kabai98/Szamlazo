using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using Szamlazo.Application.Contracts;
using Szamlazo.Application.Dtos;
using Szamlazo.Domain;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

internal sealed class DocumentReader(IConfiguration configuration) : IDocumentReader
{
    private static readonly string[] ImageExtensions = [".jfif", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff"];
    public async Task<ImportPreview> ReadAsync(string filename, Stream stream, CancellationToken request)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension is not (".csv" or ".xls" or ".xlsx" or ".pdf") && !ImageExtensions.Contains(extension))
            throw new InvalidDataException("Támogatott fájlok: JPG, PNG, WEBP, TIFF, PDF, XLS, XLSX és CSV.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request);
        deadline.CancelAfter(TimeSpan.FromSeconds(75));
        if (extension is ".csv" or ".xls" or ".xlsx")
            return SpreadsheetReader.Read(stream, extension, deadline.Token);

        var temp = Directory.CreateTempSubdirectory("szamlazo-");
        try
        {
            var input = Path.Combine(temp.FullName, "upload" + extension);
            await using (var destination = File.Create(input))
                await stream.CopyToAsync(destination, deadline.Token);
            var text = extension == ".pdf"
                ? await ReadPdf(input, temp.FullName, deadline.Token)
                : await ReadImage(input, deadline.Token);
            return ReceiptParser.Parse(text, extension == ".pdf" ? ExpenseSource.Pdf : ExpenseSource.Image);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private async Task<string> ReadPdf(string path, string workspace, CancellationToken token)
    {
        var info = await RunAsync("pdfinfo", ["-enc", "UTF-8", path], token);
        var pages = Regex.Match(info, @"(?m)^Pages:\s*(\d+)");
        if (!pages.Success || !int.TryParse(pages.Groups[1].Value, out var count) || count is < 1 or > 5)
            throw new InvalidDataException("Egy legfeljebb 5 oldalas számla PDF-jét töltsd fel.");

        var output = new StringBuilder();
        for (var number = 1; number <= count; number++)
        {
            var page = number.ToString(CultureInfo.InvariantCulture);
            var text = await RunAsync("pdftotext", ["-f", page, "-l", page, "-layout", "-enc", "UTF-8", path, "-"], token);
            if (text.Count(char.IsLetterOrDigit) < 30)
            {
                var image = Path.Combine(workspace, "page");
                await RunAsync("pdftoppm", ["-f", page, "-l", page, "-scale-to", "1400", "-singlefile", "-png", path, image], token);
                text = await ReadImage(image + ".png", token);
            }
            output.AppendLine(text);
        }
        return output.ToString();
    }

    private async Task<string> ReadImage(string path, CancellationToken token)
    {
        var prepared = path + ".prepared.png";
        await RunAsync("python3", [Path.Combine(AppContext.BaseDirectory,
            "Importing", "Tools", "prepare_receipt.py"), path, prepared], token);
        var originalText = await RunAsync("tesseract", [path, "stdout", "-l", "hun+eng", "--psm", "6"], token);
        var preparedText = await RunAsync("tesseract", [prepared, "stdout", "-l", "hun+eng", "--psm", "6"], token);
        return Score(preparedText) > Score(originalText) ? preparedText : originalText;
    }

    private static int Score(string text)
    {
        var preview = ReceiptParser.Parse(text, ExpenseSource.Image);
        var row = preview.Rows[0];
        return row.Items.Count * 2 + (row.Amount.HasValue ? 5 : 0) + (row.Date.HasValue ? 3 : 0);
    }

    private async Task<string> RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = configuration[$"Tools:{executable}"] ?? executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
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
        catch (Win32Exception)
        {
            throw new InvalidDataException("A helyi szövegfelismerő eszköz hiányzik. A Docker-változat tartalmazza.");
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(token);
            var errors = process.StandardError.ReadToEndAsync(token);
            try
            {
                await process.WaitForExitAsync(token);
                await errors;
                if (process.ExitCode != 0)
                    throw new InvalidDataException("A fájl nem olvasható. Ellenőrizd, hogy megnyitható és nincs jelszóval védve.");
                return await output;
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
        }
    }

}
