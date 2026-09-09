using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Domain.Enums
{
    public enum DocumentStatus
    {
        Pending,
        Processing,
        Chunked,
        Indexed,
        Failed
    }
}
