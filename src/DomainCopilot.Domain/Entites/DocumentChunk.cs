using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Domain.Entites
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public string Content { get; set; } = string.Empty;
        public string? ClauseOrSection { get; set; }
        public int? PageNumber { get; set; }
        public int ChunkIndex { get; set; } 
        public bool LowConfidence { get; set; } = false; 
        public Dictionary<string, string>? ExtraMetadata { get; set; } 
    }
}
