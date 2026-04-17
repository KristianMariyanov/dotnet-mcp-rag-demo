using DotNetConf.Seeder.Services;

namespace DotNetConf.Seeder.Tests;

public sealed class DevBgJobPageParserTests
{
    [Fact]
    public async Task ParseAsync_ExtractsStructuredMetadataFromLiveFixture()
    {
        var html = await File.ReadAllTextAsync(Fixture("devbg-job-detail-aristocrat.html"));
        var parser = new DevBgJobPageParser();

        var posting = await parser.ParseAsync(
            "https://dev.bg/company/jobads/aristocrat-net-integration-developer/",
            html,
            new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.FromHours(3)),
            CancellationToken.None);

        Assert.Equal("525169", posting.SourceJobId);
        Assert.Equal(".Net Integration Developer", posting.Title);
        Assert.Equal("Aristocrat", posting.CompanyName);
        Assert.Equal("София", posting.Location);
        Assert.Equal("Hybrid", posting.WorkModel);
        Assert.Equal(new DateOnly(2026, 4, 14), posting.PostedOn);
        Assert.Contains(posting.Categories, category => category.Name == ".NET");
        Assert.Contains(posting.Categories, category => category.Name == "Backend Development");
        Assert.Contains(posting.Technologies, technology => technology.Name == ".NET");
        Assert.Contains(posting.Technologies, technology => technology.Name == "ASP.NET MVC");
        Assert.Contains(posting.Technologies, technology => technology.Name == "C#");
        Assert.Contains("Main Duties and Responsibilities", posting.DescriptionText, StringComparison.Ordinal);
        Assert.Contains("MS SQL Server", posting.DescriptionText, StringComparison.Ordinal);
        Assert.Equal("Backend Development", posting.JobType);
    }

    private static string Fixture(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
