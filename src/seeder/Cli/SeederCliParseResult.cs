namespace DotNetConf.Seeder.Cli;

public sealed record SeederCliParseResult(
    bool Success,
    SeederCliOptions Options,
    IReadOnlyList<string> Errors);
