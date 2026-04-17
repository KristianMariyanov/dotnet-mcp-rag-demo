using DotNetConf.RagServer.Services;

namespace DotNetConf.RagServer.Tests;

public sealed class QuerySplitterTests
{
    private readonly QuerySplitter _splitter = new();

    [Fact]
    public void Split_ReturnsSingleQuery_WhenQuestionIsFocused()
    {
        var result = _splitter.Split("Where do they explain dependency injection in the keynote?", 4);

        Assert.Single(result);
        Assert.Equal("Where do they explain dependency injection in the keynote", result[0]);
    }

    [Fact]
    public void Split_BreaksApartMultiQuestionInput()
    {
        var result = _splitter.Split(
            "Where do they explain dependency injection? How do they register services?",
            4);

        Assert.Equal(2, result.Count);
        Assert.Contains("Where do they explain dependency injection", result);
        Assert.Contains("How do they register services", result);
    }
}
