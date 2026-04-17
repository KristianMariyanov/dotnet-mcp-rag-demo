using System.Xml.Linq;
using DotNetConf.Seeder.Models;

namespace DotNetConf.Seeder.Services;

public sealed class DevBgSitemapClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<SitemapJobReference>> DiscoverJobReferencesAsync(
        string sitemapIndexUrl,
        CancellationToken cancellationToken)
    {
        var sitemapUrls = await DiscoverJobSitemapUrlsAsync(sitemapIndexUrl, cancellationToken);
        var references = new Dictionary<string, SitemapJobReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var sitemapUrl in sitemapUrls)
        {
            var xml = await GetStringAsync(sitemapUrl, cancellationToken);
            foreach (var jobReference in ParseUrlSet(xml))
            {
                references[jobReference.Url] = jobReference;
            }
        }

        return references.Values
            .OrderBy(reference => reference.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal async Task<IReadOnlyList<string>> DiscoverJobSitemapUrlsAsync(
        string sitemapIndexUrl,
        CancellationToken cancellationToken)
    {
        var xml = await GetStringAsync(sitemapIndexUrl, cancellationToken);
        var document = XDocument.Parse(xml);

        if (document.Root?.Name.LocalName == "urlset")
        {
            return [sitemapIndexUrl];
        }

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "loc")
            .Select(element => element.Value.Trim())
            .Where(value => value.Contains("/wp-sitemap-posts-job_listing-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<SitemapJobReference> ParseUrlSet(string xml)
    {
        var document = XDocument.Parse(xml);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "url")
            .Select(urlElement =>
            {
                var loc = urlElement.Elements().FirstOrDefault(element => element.Name.LocalName == "loc")?.Value.Trim();
                var lastModRaw = urlElement.Elements().FirstOrDefault(element => element.Name.LocalName == "lastmod")?.Value.Trim();
                DateTimeOffset? lastModified = null;

                if (DateTimeOffset.TryParse(lastModRaw, out var parsedLastModified))
                {
                    lastModified = parsedLastModified;
                }

                return loc is null ? null : new SitemapJobReference(loc, lastModified);
            })
            .OfType<SitemapJobReference>()
            .ToArray();
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
