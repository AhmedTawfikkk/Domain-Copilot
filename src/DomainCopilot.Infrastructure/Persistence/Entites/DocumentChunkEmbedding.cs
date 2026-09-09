using Pgvector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Persistence.Entites
{
    public sealed class DocumentChunkEmbedding
    {
        public Guid DocumentChunkId { get; set; }

        public Vector Embedding { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
