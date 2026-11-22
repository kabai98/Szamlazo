using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Szamlazo.Domain.Enums;

namespace Szamlazo.Infrastructure.Importing;

internal sealed class ImageOcrService(IConfiguration configuration, ExternalToolRunner tools)
{
    public async Task<string> ReadAsync(string path, CancellationToken token)
    {
        if (string.Equals(configuration["Ocr:Engine"], "paddle", StringComparison.OrdinalIgnoreCase))
        {
            var json = await tools.RunAsync("python3", [Path.Combine(AppContext.BaseDirectory,
                "Importing", "Tools", "paddle_receipt.py"), path], token);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("text").GetString() ?? "";
        }

        var prepared = path + ".prepared.png";
        await tools.RunAsync("python3", [Path.Combine(AppContext.BaseDirectory,
            "Importing", "Tools", "prepare_receipt.py"), path, prepared], token);
        var bestText = "";
        var bestScore = int.MinValue;
        foreach (var candidate in new[] { path, prepared, prepared + ".binary.png" })
        {
            foreach (var mode in new[] { "4", "6" })
            {
                var text = await tools.RunAsync("tesseract", [candidate, "stdout", "-l", "hun+eng",
                    "--psm", mode, "--dpi", "300", "-c", "preserve_interword_spaces=1",
                    "-c", "load_system_dawg=0", "-c", "load_freq_dawg=0"], token);
                var score = Score(text);
                if (score > bestScore)
                {
                    bestText = text;
                    bestScore = score;
                }
            }
        }
        return bestText;
    }

    internal static int Score(string text)
    {
        var preview = ReceiptParser.Parse(text, ExpenseSource.Image);
        var row = preview.Rows[0];
        var reconciled = row.Amount is > 0 && row.Items.Count > 0 && row.Items.Sum(item => item.Amount) == row.Amount;
        // More rows can be background noise. Reward independent receipt evidence instead.
        return (reconciled ? 100 : 0) +
            (row.Category == "Élelmiszer" ? 20 : 0) +
            (row.Amount.HasValue ? 15 : 0) + (row.Date.HasValue ? 10 : 0) +
            (row.PaymentMethod is not null ? 5 : 0) - preview.Warnings.Count * 5;
    }

}
