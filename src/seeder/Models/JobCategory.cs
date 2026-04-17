namespace DotNetConf.Seeder.Models;

public sealed record JobCategory(
    string Name,
    string? Url,
    int? ListingCount);
