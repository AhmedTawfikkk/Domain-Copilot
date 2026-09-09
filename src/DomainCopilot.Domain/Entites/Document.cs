using DomainCopilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Domain.Entites
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; 
        public DocumentSourceType SourceType { get; set; } 
        public string Version { get; set; } = "1.0";
        public string FileHash { get; set; } = string.Empty; 
        public DateTime UploadedAt { get; set; }
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
        public string? FailureReason { get; set; }
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
