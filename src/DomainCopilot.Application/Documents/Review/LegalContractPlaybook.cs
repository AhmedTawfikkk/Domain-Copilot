namespace DomainCopilot.Application.Documents.Review;

public sealed class LegalContractPlaybook : IContractReviewPlaybook
{
    public RiskAssessmentResult Assess(RiskAssessmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var findings = new List<RiskFinding>();

        AssessLimitationOfLiability(request.Clauses, findings);
        AssessIndemnification(request.Clauses, findings);
        AssessConfidentiality(request.Clauses, findings);
        AssessTermination(request.Clauses, findings);
        AssessGoverningLaw(request.Clauses, findings);

        return new RiskAssessmentResult(findings);
    }

    private static void AssessLimitationOfLiability(
        IReadOnlyList<ExtractedClause> clauses,
        ICollection<RiskFinding> findings)
    {
        var liabilityClauses = FindClauses(
            clauses,
            LegalClauseType.LimitationOfLiability);

        if (liabilityClauses.Count == 0)
        {
            findings.Add(new RiskFinding(
                "PB-LIAB-001",
                LegalClauseType.LimitationOfLiability,
                RiskSeverity.High,
                "Missing limitation-of-liability clause",
                "The contract does not contain an identified limitation-of-liability clause.",
                "Add a mutual limitation-of-liability clause with a defined liability cap.",
                null));

            return;
        }

        foreach (var clause in liabilityClauses)
        {
            if (!ContainsAny(
                    clause.EvidenceText,
                    "liability cap",
                    "aggregate liability",
                    "fees paid",
                    "amount paid",
                    "insurance coverage",
                    "shall not exceed"))
            {
                findings.Add(new RiskFinding(
                    "PB-LIAB-002",
                    LegalClauseType.LimitationOfLiability,
                    RiskSeverity.High,
                    "Liability cap is not evident",
                    "An identified liability clause does not clearly state a monetary or fee-based cap.",
                    "Define an aggregate liability cap, such as fees paid during a stated period.",
                    clause.DocumentChunkId));
            }

            if (!ContainsAny(
                    clause.EvidenceText,
                    "indirect",
                    "consequential",
                    "incidental",
                    "special",
                    "punitive"))
            {
                findings.Add(new RiskFinding(
                    "PB-LIAB-003",
                    LegalClauseType.LimitationOfLiability,
                    RiskSeverity.Medium,
                    "Indirect-damages exclusion is not evident",
                    "The identified liability clause does not clearly exclude indirect or consequential damages.",
                    "Consider excluding indirect, incidental, special, and consequential damages.",
                    clause.DocumentChunkId));
            }
        }
    }

    private static void AssessIndemnification(
        IReadOnlyList<ExtractedClause> clauses,
        ICollection<RiskFinding> findings)
    {
        var indemnityClauses = FindClauses(
            clauses,
            LegalClauseType.Indemnification);

        if (indemnityClauses.Count == 0)
        {
            findings.Add(new RiskFinding(
                "PB-IND-001",
                LegalClauseType.Indemnification,
                RiskSeverity.High,
                "Missing indemnification clause",
                "The contract does not contain an identified indemnification clause.",
                "Add a clearly scoped mutual or role-appropriate indemnification clause.",
                null));

            return;
        }

        foreach (var clause in indemnityClauses)
        {
            if (!ContainsAny(
                    clause.EvidenceText,
                    "defend",
                    "defense",
                    "defence"))
            {
                findings.Add(new RiskFinding(
                    "PB-IND-002",
                    LegalClauseType.Indemnification,
                    RiskSeverity.Medium,
                    "Indemnity defense obligation is not evident",
                    "The indemnity clause does not clearly state who controls or funds the defense.",
                    "Specify the indemnifying party's duty to defend and the defense-control process.",
                    clause.DocumentChunkId));
            }
        }
    }

    private static void AssessConfidentiality(
        IReadOnlyList<ExtractedClause> clauses,
        ICollection<RiskFinding> findings)
    {
        var confidentialityClauses = FindClauses(
            clauses,
            LegalClauseType.Confidentiality);

        if (confidentialityClauses.Count == 0)
        {
            findings.Add(new RiskFinding(
                "PB-CONF-001",
                LegalClauseType.Confidentiality,
                RiskSeverity.High,
                "Missing confidentiality clause",
                "The contract does not contain an identified confidentiality clause.",
                "Add mutual confidentiality obligations and permitted-disclosure exceptions.",
                null));

            return;
        }

        foreach (var clause in confidentialityClauses)
        {
            if (!ContainsAny(
                    clause.EvidenceText,
                    "survive",
                    "survival",
                    "after termination",
                    "following termination"))
            {
                findings.Add(new RiskFinding(
                    "PB-CONF-002",
                    LegalClauseType.Confidentiality,
                    RiskSeverity.Medium,
                    "Confidentiality survival is not evident",
                    "The confidentiality clause does not clearly state whether obligations survive termination.",
                    "Specify an appropriate survival period for confidentiality obligations.",
                    clause.DocumentChunkId));
            }
        }
    }

    private static void AssessTermination(
        IReadOnlyList<ExtractedClause> clauses,
        ICollection<RiskFinding> findings)
    {
        var terminationClauses = FindClauses(
            clauses,
            LegalClauseType.Termination);

        if (terminationClauses.Count == 0)
        {
            findings.Add(new RiskFinding(
                "PB-TERM-001",
                LegalClauseType.Termination,
                RiskSeverity.High,
                "Missing termination clause",
                "The contract does not contain an identified termination clause.",
                "Add termination-for-cause and termination-for-convenience provisions where appropriate.",
                null));

            return;
        }

        foreach (var clause in terminationClauses)
        {
            if (!ContainsAny(
                    clause.EvidenceText,
                    "notice",
                    "days' notice",
                    "written notice"))
            {
                findings.Add(new RiskFinding(
                    "PB-TERM-002",
                    LegalClauseType.Termination,
                    RiskSeverity.Medium,
                    "Termination notice requirement is not evident",
                    "The termination clause does not clearly specify a notice requirement.",
                    "Specify notice form and notice period for termination.",
                    clause.DocumentChunkId));
            }
        }
    }

    private static void AssessGoverningLaw(
        IReadOnlyList<ExtractedClause> clauses,
        ICollection<RiskFinding> findings)
    {
        var governingLawClauses = FindClauses(
            clauses,
            LegalClauseType.GoverningLaw);

        if (governingLawClauses.Count == 0)
        {
            findings.Add(new RiskFinding(
                "PB-GOV-001",
                LegalClauseType.GoverningLaw,
                RiskSeverity.Medium,
                "Missing governing-law clause",
                "The contract does not contain an identified governing-law clause.",
                "Specify the governing law and, where needed, dispute-resolution venue.",
                null));

            return;
        }

        foreach (var clause in governingLawClauses)
        {
            if (!ContainsAny(
                    clause.EvidenceText,
                    "laws of",
                    "governed by",
                    "jurisdiction",
                    "courts of"))
            {
                findings.Add(new RiskFinding(
                    "PB-GOV-002",
                    LegalClauseType.GoverningLaw,
                    RiskSeverity.Medium,
                    "Governing-law jurisdiction is unclear",
                    "The identified clause does not clearly state a governing law or forum.",
                    "Specify governing law and the agreed jurisdiction or venue.",
                    clause.DocumentChunkId));
            }
        }
    }

    private static IReadOnlyList<ExtractedClause> FindClauses(
        IReadOnlyList<ExtractedClause> clauses,
        LegalClauseType clauseType)
    {
        return clauses
            .Where(clause => clause.ClauseType == clauseType)
            .ToList();
    }

    private static bool ContainsAny(
        string value,
        params string[] terms)
    {
        return terms.Any(term =>
            value.Contains(
                term,
                StringComparison.OrdinalIgnoreCase));
    }
}