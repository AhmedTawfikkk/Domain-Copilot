using DomainCopilot.Application.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class LegalTextCleaner : ITextCleaner
    {
        public string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\u00A0", " ");

            cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
            cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

            return cleaned.Trim();
        }
    }
}