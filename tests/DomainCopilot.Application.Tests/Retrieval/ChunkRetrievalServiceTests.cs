using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Application.Providers;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Tests.Retrieval
{
    public class ChunkRetrievalServiceTests
    {
        [Fact]
        public async Task SearchAsync_Hybrid_UsesReciprocalRankFusion()
        {
            var chunkA = CreateChunk(Guid.NewGuid(), "A");
            var chunkB = CreateChunk(Guid.NewGuid(), "B");
            var chunkC = CreateChunk(Guid.NewGuid(), "C");

            var repository = new Mock<IChunkRetrievalRepository>();

            repository.Setup(repository =>
                    repository.SearchDenseAsync(
                        It.IsAny<float[]>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chunkA, chunkB });

            repository.Setup(repository =>
                    repository.SearchKeywordAsync(
                        It.IsAny<string>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chunkB, chunkC });

            var llmProvider = new Mock<ILlmProvider>();

            llmProvider.Setup(provider =>
                    provider.EmbedAsync(
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            var service = new ChunkRetrievalService(
                repository.Object,
                llmProvider.Object);

            var results = await service.SearchAsync(
                "limitation of liability",
                RetreivalMode.Hybrid,
                limit: 3);

            Assert.Equal(chunkB.DocumentChunkId, results[0].DocumentChunkId);
            Assert.Equal(3, results.Count);
        }

        private static RetrievedChunk CreateChunk(
            Guid chunkId,
            string content)
        {
            return new RetrievedChunk(
                chunkId,
                Guid.NewGuid(),
                "sample-contract.txt",
                "Test",
                "1.0",
                content,
                null,
                1,
                false,
                0);
        }
    }
}
