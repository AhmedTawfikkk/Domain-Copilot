using DomainCopilot.Application.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class PlainTextExtractor : ITextExtractor
    {
        public bool CanExtract(string fileName) =>
            Path.GetExtension(fileName)
                .Equals(".txt", StringComparison.OrdinalIgnoreCase);

        public async Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(
                content,
                leaveOpen: true);

            var text = await reader.ReadToEndAsync(cancellationToken);

            return
            [
                new ExtractedPage(1, text)
            ];
        }
    }
}
