namespace DotNetConf.Seeder.Models;

public sealed record SitemapJobReference(
    string Url,
    DateTimeOffset? LastModified);
