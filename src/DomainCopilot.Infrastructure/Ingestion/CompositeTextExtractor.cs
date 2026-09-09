using DomainCopilot.Application.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class CompositeTextExtractor:IDocumentTextExtractor
    {
        private readonly IReadOnlyCollection<ITextExtractor> _extractors;

        public CompositeTextExtractor(IEnumerable<ITextExtractor> extractors)
        {
            _extractors = extractors.ToList();
        }

        public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            var extractor = _extractors.FirstOrDefault(
                candidate => candidate.CanExtract(fileName));

            if (extractor is null)
            {
                throw new NotSupportedException(
                    $"Unsupported file type: {Path.GetExtension(fileName)}");
            }

            return extractor.ExtractAsync(content, cancellationToken);
        }
    }
}
