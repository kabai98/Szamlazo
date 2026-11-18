using Szamlazo.Application.Dtos;

namespace Szamlazo.Application.Contracts;

public interface IDocumentReader
{
    Task<ImportPreview> ReadAsync(string filename, Stream content, CancellationToken token);
}
