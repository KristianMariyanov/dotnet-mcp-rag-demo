namespace DotNetConf.Seeder.Cli;

public sealed record SeederCliOptions(
    string DatabasePath,
    string SitemapIndexUrl,
    int Concurrency,
    int? Limit,
    bool ValidateOnly,
    bool SkipValidation,
    bool ShowHelp);
