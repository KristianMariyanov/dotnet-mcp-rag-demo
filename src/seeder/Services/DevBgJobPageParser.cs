using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DotNetConf.Seeder.Models;
using DotNetConf.Seeder.Support;

namespace DotNetConf.Seeder.Services;

public sealed partial class DevBgJobPageParser
{
    private readonly HtmlParser _htmlParser = new();

    public async Task<DevBgJobPosting> ParseAsync(
        string pageUrl,
        string html,
        DateTimeOffset? sitemapLastModified,
        CancellationToken cancellationToken)
    {
        var document = await _htmlParser.ParseDocumentAsync(html, cancellationToken);

        var canonicalUrl = FirstNonEmpty(
            document.QuerySelector("link[rel='canonical']")?.GetAttribute("href"),
            pageUrl);

        var title = TextNormalizer.Normalize(document.QuerySelector("h1.job-title")?.TextContent);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException($"Job title was not found for {pageUrl}.");
        }

        var dataLayer = ParseDataLayer(html);
        var schema = ParseSchema(document);

        var sourceJobId = dataLayer.JobId?.ToString() ?? MatchFirstGroup(html, JobIdRegex());
        if (string.IsNullOrWhiteSpace(sourceJobId))
        {
            throw new InvalidOperationException($"A stable job id was not found for {pageUrl}.");
        }

        var descriptionNode = document.QuerySelector(".single_job_listing .job_description");

        var categories = document.QuerySelectorAll(".categories-wrap a.pill")
            .Select(ParseCategory)
            .Where(category => category is not null)
            .Cast<JobCategory>()
            .ToArray();

        var technologies = document.QuerySelectorAll(".single_job_listing .component-square-badge img[title]")
            .Select(element => TextNormalizer.Normalize(element.GetAttribute("title")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new JobTechnology(name!, index))
            .ToArray();

        var companyAside = document.QuerySelector("aside.right-sidebar-company");
        var companyJobsButton = companyAside?.QuerySelector("a[href$='#jobs']");
        var companySummary = companyAside?.QuerySelectorAll("p")
            .Select(paragraph => TextNormalizer.Normalize(paragraph.TextContent))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        var companyUrl = ToAbsoluteUrl(pageUrl, document.QuerySelector(".company-logo-link")?.GetAttribute("href"));
        var companyJobsUrl = ToAbsoluteUrl(pageUrl, companyJobsButton?.GetAttribute("href"));
        var companyOpenJobsCount = ParseNullableInt(
            companyJobsButton?.QuerySelector(".jobs-number-in-btn")?.TextContent);

        var locationValues = document.QuerySelectorAll(".tags-wrap .badge > a")
            .Select(anchor => TextNormalizer.Normalize(anchor.TextContent))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workModel = FirstNonEmpty(
            dataLayer.WorkFrom,
            TextNormalizer.Normalize(document.QuerySelector(".tags-wrap .badge .suffix-hybrid")?.TextContent),
            TextNormalizer.Normalize(document.QuerySelector(".badges-wrap")?.TextContent));

        var workModelDetails = TextNormalizer.Normalize(
            document.QuerySelector(".tags-wrap .badge .badge-tooltip")?.TextContent);

        var postedOn = ParseDateOnly(
            document.QuerySelector(".job-listing-meta time")?.GetAttribute("datetime")) ??
            ParseDateOnly(schema.DatePosted);

        var validThrough = ParseDateOnly(schema.ValidThrough);
        var salaryText = ExtractSalaryText(document);
        var ogDescription = document.QuerySelector("meta[property='og:description']")?.GetAttribute("content")?.Trim();
        var descriptionText = TextNormalizer.Normalize(descriptionNode?.TextContent)
            ?? TextNormalizer.Normalize(schema.Description)
            ?? TextNormalizer.Normalize(ogDescription);

        if (string.IsNullOrWhiteSpace(descriptionText))
        {
            throw new InvalidOperationException($"The job description was not found for {pageUrl}.");
        }

        var descriptionHtml = descriptionNode?.InnerHtml.Trim() ?? $"<p>{System.Net.WebUtility.HtmlEncode(descriptionText)}</p>";

        var metadataJson = BuildMetadataJson(
            pageUrl,
            canonicalUrl!,
            dataLayer,
            schema,
            categories,
            technologies,
            locationValues,
            workModelDetails,
            companySummary,
            ogDescription);

        return new DevBgJobPosting(
            SourceJobId: sourceJobId,
            Url: pageUrl,
            CanonicalUrl: canonicalUrl!,
            Slug: GetSlug(canonicalUrl!),
            Title: title!,
            CompanyName: FirstNonEmpty(
                TextNormalizer.Normalize(document.QuerySelector(".company-name")?.TextContent),
                dataLayer.CompanyName,
                schema.HiringOrganizationName),
            CompanyUrl: companyUrl,
            CompanyLogoUrl: ToAbsoluteUrl(pageUrl, document.QuerySelector(".company-logo-link img")?.GetAttribute("src")),
            CompanySummary: companySummary,
            CompanyJobsUrl: companyJobsUrl,
            CompanyOpenJobsCount: companyOpenJobsCount,
            Location: locationValues.Length == 0 ? null : string.Join(", ", locationValues),
            WorkModel: workModel,
            WorkModelDetails: workModelDetails,
            PostedOn: postedOn,
            ValidThrough: validThrough,
            EmploymentType: schema.EmploymentType,
            JobType: dataLayer.JobType,
            JobSubType: dataLayer.JobSubType,
            PaymentType: dataLayer.PaymentType,
            OnlyInDevBg: dataLayer.OnlyInDevBg,
            SenioritySignal: dataLayer.IsJuniorPosition,
            QualityScore: dataLayer.QualityScore,
            ValueScore: dataLayer.ValueScore,
            SalaryText: salaryText,
            OgDescription: ogDescription,
            DescriptionHtml: descriptionHtml,
            DescriptionText: descriptionText,
            SitemapLastModified: sitemapLastModified,
            Categories: categories,
            Technologies: technologies,
            MetadataJson: metadataJson);
    }

    internal static JobCategory? ParseCategory(IElement element)
    {
        var name = TextNormalizer.Normalize(
            element.ChildNodes
                .Where(node => node.NodeType == NodeType.Text)
                .Select(node => node.TextContent)
                .DefaultIfEmpty(element.TextContent)
                .Aggregate(string.Concat));

        if (string.IsNullOrWhiteSpace(name))
        {
            name = TextNormalizer.Normalize(element.TextContent);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new JobCategory(
            name!,
            element.GetAttribute("href"),
            ParseNullableInt(element.QuerySelector(".count")?.TextContent));
    }

    private static string? ExtractSalaryText(IDocument document)
    {
        var candidates = document.All
            .Where(element =>
                ContainsIgnoreCase(element.ClassName, "salary") ||
                ContainsIgnoreCase(element.Id, "salary"))
            .Select(element => TextNormalizer.Normalize(element.TextContent))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(text => text!.Length <= 120)
            .ToArray();

        return candidates.Length == 0 ? null : string.Join(" | ", candidates);
    }

    private static bool ContainsIgnoreCase(string? value, string expected) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static string BuildMetadataJson(
        string pageUrl,
        string canonicalUrl,
        DataLayerMetadata dataLayer,
        JsonLdMetadata schema,
        IReadOnlyList<JobCategory> categories,
        IReadOnlyList<JobTechnology> technologies,
        IReadOnlyList<string> locations,
        string? workModelDetails,
        string? companySummary,
        string? ogDescription)
    {
        var payload = new
        {
            sourceSite = "dev.bg",
            pageUrl,
            canonicalUrl,
            locations,
            workModelDetails,
            companySummary,
            ogDescription,
            dataLayer,
            schema,
            categories,
            technologies
        };

        return JsonSerializer.Serialize(payload);
    }

    private static JsonLdMetadata ParseSchema(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var text = script.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                continue;
            }

            if (node is not JsonObject jobPosting)
            {
                continue;
            }

            if (!string.Equals(jobPosting["@type"]?.GetValue<string>(), "JobPosting", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new JsonLdMetadata(
                jobPosting["datePosted"]?.GetValue<string>(),
                jobPosting["validThrough"]?.GetValue<string>(),
                jobPosting["employmentType"]?.GetValue<string>(),
                jobPosting["hiringOrganization"]?["sameAs"]?.GetValue<string>(),
                jobPosting["description"]?.GetValue<string>());
        }

        return JsonLdMetadata.Empty;
    }

    private static DataLayerMetadata ParseDataLayer(string html) =>
        new(
            MatchFirstGroup(html, JobPositionRegex()),
            ParseNullableInt(MatchFirstGroup(html, JobIdRegex())),
            MatchFirstGroup(html, CompanyNameRegex()),
            MatchFirstGroup(html, JobTypeRegex()),
            MatchFirstGroup(html, JobSubTypeRegex()),
            MatchFirstGroup(html, PaymentTypeRegex()),
            MatchFirstGroup(html, WorkFromRegex()),
            MatchFirstGroup(html, OnlyInDevBgRegex()),
            MatchFirstGroup(html, IsJuniorPositionRegex()),
            ParseNullableInt(MatchFirstGroup(html, QualityScoreRegex())),
            ParseNullableInt(MatchFirstGroup(html, ValueRegex())));

    private static string? MatchFirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? ToAbsoluteUrl(string baseUrl, string? maybeRelative)
    {
        if (string.IsNullOrWhiteSpace(maybeRelative))
        {
            return null;
        }

        return Uri.TryCreate(new Uri(baseUrl), maybeRelative, out var uri)
            ? uri.ToString()
            : maybeRelative;
    }

    private static int? ParseNullableInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digitsOnly, out var value) ? value : null;
    }

    private static DateOnly? ParseDateOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateOnly.TryParse(raw, out var dateOnly))
        {
            return dateOnly;
        }

        return DateTimeOffset.TryParse(raw, out var dateTimeOffset)
            ? DateOnly.FromDateTime(dateTimeOffset.Date)
            : null;
    }

    private static string GetSlug(string url)
    {
        var uri = new Uri(url);
        var segments = uri.Segments
            .Select(segment => segment.Trim('/'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return segments.Length == 0 ? string.Empty : segments[^1];
    }

    [GeneratedRegex("\"jobPosition\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex JobPositionRegex();

    [GeneratedRegex("\"jobId\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Singleline)]
    private static partial Regex JobIdRegex();

    [GeneratedRegex("\"companyName\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex CompanyNameRegex();

    [GeneratedRegex("\"jobType\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex JobTypeRegex();

    [GeneratedRegex("\"jobSubType\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex JobSubTypeRegex();

    [GeneratedRegex("\"paymentType\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex PaymentTypeRegex();

    [GeneratedRegex("\"workFrom\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex WorkFromRegex();

    [GeneratedRegex("\"onlyInDevBg\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex OnlyInDevBgRegex();

    [GeneratedRegex("\"isJuniorPosition\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Singleline)]
    private static partial Regex IsJuniorPositionRegex();

    [GeneratedRegex("\"qualityScore\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Singleline)]
    private static partial Regex QualityScoreRegex();

    [GeneratedRegex("\"value\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Singleline)]
    private static partial Regex ValueRegex();

    internal sealed record DataLayerMetadata(
        string? JobPosition,
        int? JobId,
        string? CompanyName,
        string? JobType,
        string? JobSubType,
        string? PaymentType,
        string? WorkFrom,
        string? OnlyInDevBg,
        string? IsJuniorPosition,
        int? QualityScore,
        int? ValueScore);

    internal sealed record JsonLdMetadata(
        string? DatePosted,
        string? ValidThrough,
        string? EmploymentType,
        string? HiringOrganizationName,
        string? Description)
    {
        public static JsonLdMetadata Empty { get; } = new(null, null, null, null, null);
    }
}
