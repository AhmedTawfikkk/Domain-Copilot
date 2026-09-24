namespace DomainCopilot.Application.Documents.Access;

/// <summary>
/// Resolves the owner of a document so that object-level authorization can
/// be enforced (OWASP Broken Access Control). Runs and memos inherit their
/// ownership from the document they belong to.
/// </summary>
public interface IDocumentOwnershipRepository
{
    /// <summary>
    /// Returns the owner user id of the given document, or <c>null</c> when the
    /// document does not exist or has never been assigned an owner.
    /// </summary>
    Task<Guid?> GetOwnerIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}