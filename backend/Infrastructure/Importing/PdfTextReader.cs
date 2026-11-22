using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Szamlazo.Infrastructure.Importing;

internal sealed class PdfTextReader(ExternalToolRunner tools, ImageOcrService images)
{
    public async Task<string> ReadAsync(string path, string workspace, CancellationToken token)
    {
        var info = await tools.RunAsync("pdfinfo", ["-enc", "UTF-8", path], token);
        var pages = Regex.Match(info, @"(?m)^Pages:\s*(\d+)");
        if (!pages.Success || !int.TryParse(pages.Groups[1].Value, out var count) || count is < 1 or > 5)
            throw new InvalidDataException("Egy legfeljebb 5 oldalas számla PDF-jét töltsd fel.");

        var output = new StringBuilder();
        for (var number = 1; number <= count; number++)
        {
            var page = number.ToString(CultureInfo.InvariantCulture);
            var text = await tools.RunAsync("pdftotext", ["-f", page, "-l", page, "-layout", "-enc", "UTF-8", path, "-"], token);
            if (text.Count(char.IsLetterOrDigit) < 30)
            {
                var image = Path.Combine(workspace, "page");
                await tools.RunAsync("pdftoppm", ["-f", page, "-l", page, "-scale-to", "1400", "-singlefile", "-png", path, image], token);
                text = await images.ReadAsync(image + ".png", token);
            }
            output.AppendLine(text);
        }
        return output.ToString();
    }

}
