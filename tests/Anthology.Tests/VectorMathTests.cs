using Anthology.Modules.Recommendations;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public sealed class VectorMathTests
{
    [Fact]
    public void Identical_vectors_have_zero_distance()
    {
        var v = new[] { 1f, 2f, 3f };
        VectorMath.CosineDistance(v, v).Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void Orthogonal_vectors_have_distance_one()
    {
        VectorMath.CosineDistance([1f, 0f], [0f, 1f]).Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Opposite_vectors_have_distance_two()
    {
        VectorMath.CosineDistance([1f, 0f], [-1f, 0f]).Should().BeApproximately(2f, 1e-5f);
    }
}
