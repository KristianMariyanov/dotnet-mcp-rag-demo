using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using DotNetConf.Seeder.Models;
using DotNetConf.Seeder.Support;

namespace DotNetConf.Seeder.Services.Chunking;

public sealed partial class JobChunkBuilder
{
    private const int TargetChunkLength = 900;
    private readonly HtmlParser _htmlParser = new();

    public async Task<IReadOnlyList<JobChunkRecord>> BuildAsync(
        DevBgJobPosting posting,
        CancellationToken cancellationToken)
    {
        var chunks = new List<JobChunkRecord>
        {
            CreateOverviewChunk(posting)
        };

        var blockChunks = await BuildDescriptionChunksAsync(posting, cancellationToken);
        chunks.AddRange(blockChunks);

        return chunks
            .Select((chunk, index) => chunk with
            {
                ChunkId = CreateChunkId(posting.SourceJobId, index),
                ChunkIndex = index
            })
            .ToArray();
    }

    private JobChunkRecord CreateOverviewChunk(DevBgJobPosting posting)
    {
        var lines = new List<string>
        {
            posting.Title
        };

        AddLine(lines, posting.CompanyName, "Company");
        AddLine(lines, posting.Location, "Location");
        AddLine(lines, posting.WorkModel, "Work model");
        AddLine(lines, posting.EmploymentType, "Employment type");
        AddLine(lines, posting.JobType, "Job type");
        AddLine(lines, posting.JobSubType, "Specialization");
        AddLine(lines, posting.SalaryText, "Salary");

        if (posting.Categories.Count > 0)
        {
            lines.Add($"Categories: {string.Join(", ", posting.Categories.Select(category => category.Name))}");
        }

        if (posting.Technologies.Count > 0)
        {
            lines.Add($"Technologies: {string.Join(", ", posting.Technologies.Select(technology => technology.Name))}");
        }

        AddLine(lines, posting.CompanySummary, "Company summary");
        AddLine(lines, posting.OgDescription, "Overview");

        var chunkText = string.Join(" ", lines);
        return new JobChunkRecord(
            ChunkId: string.Empty,
            SourceJobId: posting.SourceJobId,
            ChunkIndex: -1,
            Section: "overview",
            ChunkText: chunkText,
            SearchText: BuildSearchText(posting, "overview", chunkText));
    }

    private async Task<IReadOnlyList<JobChunkRecord>> BuildDescriptionChunksAsync(
        DevBgJobPosting posting,
        CancellationToken cancellationToken)
    {
        var blocks = await ExtractBlocksAsync(posting.DescriptionHtml, cancellationToken);
        if (blocks.Count == 0)
        {
            blocks = SplitFallbackBlocks(posting.DescriptionText);
        }

        var records = new List<JobChunkRecord>();
        var buffer = new StringBuilder();
        var currentSection = "description";

        foreach (var block in blocks)
        {
            var candidateText = buffer.Length == 0
                ? block.Text
                : $"{buffer} {block.Text}";

            if (buffer.Length > 0 && candidateText.Length > TargetChunkLength)
            {
                records.Add(CreateChunk(posting, currentSection, buffer.ToString()));
                buffer.Clear();
                currentSection = block.Section;
            }

            if (buffer.Length == 0)
            {
                currentSection = block.Section;
                buffer.Append(block.Text);
                continue;
            }

            buffer.Append(' ').Append(block.Text);
        }

        if (buffer.Length > 0)
        {
            records.Add(CreateChunk(posting, currentSection, buffer.ToString()));
        }

        return records;
    }

    private async Task<List<ChunkBlock>> ExtractBlocksAsync(string descriptionHtml, CancellationToken cancellationToken)
    {
        var document = await _htmlParser.ParseDocumentAsync($"<body>{descriptionHtml}</body>", cancellationToken);
        var blocks = new List<ChunkBlock>();
        string? currentHeading = null;
        var body = document.Body;
        if (body is null)
        {
            return blocks;
        }

        foreach (var element in body.Children)
        {
            VisitElement(element, blocks, ref currentHeading);
        }

        return blocks;
    }

    private static void VisitElement(
        AngleSharp.Dom.IElement element,
        List<ChunkBlock> blocks,
        ref string? currentHeading)
    {
        if (IsHeading(element.LocalName))
        {
            currentHeading = TextNormalizer.Normalize(element.TextContent);
            return;
        }

        if (element.LocalName is "p" or "li")
        {
            var text = TextNormalizer.Normalize(element.TextContent);
            if (!string.IsNullOrWhiteSpace(text))
            {
                blocks.Add(new ChunkBlock(currentHeading ?? "description", PrefixWithHeading(currentHeading, text)));
            }

            return;
        }

        foreach (var child in element.Children)
        {
            VisitElement(child, blocks, ref currentHeading);
        }
    }

    private static bool IsHeading(string localName) =>
        localName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    private static string PrefixWithHeading(string? heading, string text) =>
        string.IsNullOrWhiteSpace(heading) ? text : $"{heading}: {text}";

    private static List<ChunkBlock> SplitFallbackBlocks(string descriptionText)
    {
        return SentenceRegex()
            .Split(descriptionText)
            .Select(TextNormalizer.Normalize)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => new ChunkBlock("description", text!))
            .ToList();
    }

    private static JobChunkRecord CreateChunk(DevBgJobPosting posting, string section, string chunkText)
    {
        return new JobChunkRecord(
            ChunkId: string.Empty,
            SourceJobId: posting.SourceJobId,
            ChunkIndex: -1,
            Section: TextNormalizer.Normalize(section) ?? "description",
            ChunkText: chunkText,
            SearchText: BuildSearchText(posting, section, chunkText));
    }

    private static string BuildSearchText(DevBgJobPosting posting, string section, string chunkText)
    {
        var parts = new List<string>
        {
            posting.Title,
            posting.CompanyName ?? string.Empty,
            posting.Location ?? string.Empty,
            posting.WorkModel ?? string.Empty,
            posting.EmploymentType ?? string.Empty,
            posting.JobType ?? string.Empty,
            posting.JobSubType ?? string.Empty,
            posting.SalaryText ?? string.Empty,
            section,
            string.Join(", ", posting.Categories.Select(category => category.Name)),
            string.Join(", ", posting.Technologies.Select(technology => technology.Name)),
            chunkText
        };

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string CreateChunkId(string jobId, int chunkIndex) => $"devbg:{jobId}:{chunkIndex:D3}";

    private static void AddLine(List<string> lines, string? value, string label)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    [GeneratedRegex(@"(?<=[\.\!\?])\s+")]
    private static partial Regex SentenceRegex();

    private sealed record ChunkBlock(string Section, string Text);
}
