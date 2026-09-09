using DomainCopilot.Application.Documents;
using DomainCopilot.Infrastructure.Ingestion;
using Xunit;

namespace DomainCopilot.Application.Tests.Ingestion;

public class ClauseAwareChunkerTests
{
    private readonly ClauseAwareChunker _sut = new();

    [Fact]
    public void Chunk_WithNumberedClauses_SplitsOnEachHeading()
    {
        var pages = new List<ExtractedPage>
        {
            new(1, "1. Definitions\nThis clause defines terms used herein.\n\n2. Term\nThis contract lasts 12 months.")
        };

        var chunks = _sut.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Contains("Definitions", chunks[0].ClauseOrSection);
        Assert.Contains("Term", chunks[1].ClauseOrSection);
    }

    [Fact]
    public void Chunk_WithNestedNumbering_RecognisesSubClauses()
    {
        var pages = new List<ExtractedPage>
        {
            new(1, "4.1 Limitation of Liability\nLiability is capped at fees paid.\n\n4.2 Exceptions\nExceptions apply for gross negligence.")
        };

        var chunks = _sut.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Contains("4.1", chunks[0].ClauseOrSection);
        Assert.Contains("4.2", chunks[1].ClauseOrSection);
    }

    [Fact]
    public void Chunk_WithNoRecognisableHeadings_FallsBackToWholePageChunk()
    {
        var pages = new List<ExtractedPage>
        {
            new(1, "This page has no numbered clauses at all, just plain prose text.")
        };

        var chunks = _sut.Chunk(pages);

        Assert.Single(chunks);
        Assert.Null(chunks[0].ClauseOrSection);
    }

    [Fact]
    public void Chunk_WithEmptyPage_ProducesNoChunks()
    {
        var pages = new List<ExtractedPage> { new(1, "") };

        var chunks = _sut.Chunk(pages);

        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_AssignsSequentialChunkIndexAcrossPages()
    {
        var pages = new List<ExtractedPage>
        {
            new(1, "1. First\nContent one.\n\n2. Second\nContent two."),
            new(2, "3. Third\nContent three.")
        };

        var chunks = _sut.Chunk(pages);

        Assert.Equal(new[] { 0, 1, 2 }, chunks.Select(c => c.ChunkIndex));
    }
}
