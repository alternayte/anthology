using Anthology.Workers;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public sealed class EmbeddingWorkerTests
{
    [Fact]
    public void BuildEmbeddingText_concatenates_all_fields()
    {
        var text = EmbeddingWorker.BuildEmbeddingText(
            "Interstellar",
            ["Science Fiction", "Drama"],
            ["space travel", "wormhole"],
            "A team of explorers travel through a wormhole.");

        text.Should().Be("Interstellar. Science Fiction, Drama. space travel, wormhole. A team of explorers travel through a wormhole.");
    }

    [Fact]
    public void BuildEmbeddingText_handles_null_fields()
    {
        var text = EmbeddingWorker.BuildEmbeddingText("Inception", null, null, null);
        text.Should().Be("Inception");
    }
}
