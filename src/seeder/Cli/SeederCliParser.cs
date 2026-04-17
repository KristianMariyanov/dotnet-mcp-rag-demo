namespace DotNetConf.Seeder.Cli;

public static class SeederCliParser
{
    public static SeederCliParseResult Parse(string[] args)
    {
        var errors = new List<string>();
        var databasePath = Path.Combine("artifacts", "devbg-jobs.db");
        var sitemapIndexUrl = "https://dev.bg/wp-sitemap.xml";
        var concurrency = 8;
        int? limit = null;
        var validateOnly = false;
        var skipValidation = false;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--database":
                    databasePath = ReadValue(args, ref index, "--database", errors) ?? databasePath;
                    break;
                case "--sitemap-index-url":
                    sitemapIndexUrl = ReadValue(args, ref index, "--sitemap-index-url", errors) ?? sitemapIndexUrl;
                    break;
                case "--concurrency":
                    ParseInt(ReadValue(args, ref index, "--concurrency", errors), "--concurrency", errors, value => concurrency = value);
                    break;
                case "--limit":
                    ParseInt(ReadValue(args, ref index, "--limit", errors), "--limit", errors, value => limit = value);
                    break;
                case "--validate-only":
                    validateOnly = true;
                    break;
                case "--skip-validation":
                    skipValidation = true;
                    break;
                default:
                    errors.Add($"Unknown argument '{argument}'.");
                    break;
            }
        }

        if (concurrency <= 0)
        {
            errors.Add("--concurrency must be greater than 0.");
        }

        if (limit is <= 0)
        {
            errors.Add("--limit must be greater than 0.");
        }

        if (!Uri.TryCreate(sitemapIndexUrl, UriKind.Absolute, out _))
        {
            errors.Add("--sitemap-index-url must be an absolute URL.");
        }

        return new SeederCliParseResult(
            errors.Count == 0,
            new SeederCliOptions(databasePath, sitemapIndexUrl, concurrency, limit, validateOnly, skipValidation, showHelp),
            errors);
    }

    public static string GetUsage() =>
        """
        Usage:
          dotnet run --project src/seeder -- [options]

        Options:
          --database <path>           SQLite database path. Default: artifacts/devbg-jobs.db
          --sitemap-index-url <url>   DEV.BG sitemap index URL. Default: https://dev.bg/wp-sitemap.xml
          --concurrency <count>       Number of parallel page fetches. Default: 8
          --limit <count>             Optional cap on sitemap jobs processed
          --validate-only             Skip scraping and validate the current database against the live sitemap
          --skip-validation           Skip the validation pass after scraping
          --help, -h                  Show this help text
        """;

    private static string? ReadValue(string[] args, ref int index, string optionName, List<string> errors)
    {
        if (index + 1 >= args.Length)
        {
            errors.Add($"{optionName} requires a value.");
            return null;
        }

        index++;
        return args[index];
    }

    private static void ParseInt(string? rawValue, string optionName, List<string> errors, Action<int> assign)
    {
        if (rawValue is null)
        {
            return;
        }

        if (!int.TryParse(rawValue, out var value))
        {
            errors.Add($"{optionName} must be a whole number.");
            return;
        }

        assign(value);
    }
}
