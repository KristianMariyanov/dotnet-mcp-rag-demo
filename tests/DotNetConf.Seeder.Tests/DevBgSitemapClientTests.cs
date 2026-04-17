using DotNetConf.Seeder.Services;

namespace DotNetConf.Seeder.Tests;

public sealed class DevBgSitemapClientTests
{
    [Fact]
    public void DiscoverJobSitemapUrls_FindsJobListingSitemapsFromIndex()
    {
        var xml = File.ReadAllText(Fixture("devbg-sitemap-index.xml"));
        var document = System.Xml.Linq.XDocument.Parse(xml);

        var sitemapUrls = document
            .Descendants()
            .Where(element => element.Name.LocalName == "loc")
            .Select(element => element.Value.Trim())
            .Where(value => value.Contains("job_listing", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Contains("https://dev.bg/wp-sitemap-posts-job_listing-1.xml", sitemapUrls);
    }

    [Fact]
    public void ParseUrlSet_ReadsLiveJobUrls()
    {
        var xml = File.ReadAllText(Fixture("devbg-job-sitemap.xml"));

        var references = DevBgSitemapClient.ParseUrlSet(xml);

        Assert.NotEmpty(references);
        Assert.Contains(
            references,
            reference => reference.Url == "https://dev.bg/company/jobads/aristocrat-net-integration-developer/");
    }

    private static string Fixture(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
