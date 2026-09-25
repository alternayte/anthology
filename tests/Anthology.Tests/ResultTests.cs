using Anthology.Kernel;
using FluentAssertions;
using Xunit;

namespace Anthology.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_result_holds_value()
    {
        Result<int> result = 42;
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Error_result_holds_error()
    {
        Result<int> result = Error.NotFound("test.not_found", "Not found");
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("test.not_found");
    }

    [Fact]
    public void Match_routes_to_ok()
    {
        Result<int> result = 42;
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Code}");
        output.Should().Be("ok:42");
    }

    [Fact]
    public void Match_routes_to_error()
    {
        Result<int> result = Error.Conflict("c", "conflict");
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Code}");
        output.Should().Be("err:c");
    }

    [Fact]
    public void FromError_creates_error_result()
    {
        var result = Result<string>.FromError(Error.Forbidden("f", "forbidden"));
        result.IsError.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
