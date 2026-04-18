using DotNetConf.RagServer.Contracts;

namespace DotNetConf.RagServer.Services;

public sealed class JobFitAdvisor
{
    public JobFitAnalysis Analyze(
        string query,
        string candidateProfile,
        RetrievalFilters filters,
        AggregatedCandidate candidate)
    {
        var jobTechnologies = JobMetadataParser.Split(candidate.Record.Technologies);

        var matched = new List<string>();
        var missing = new List<string>();

        foreach (var tech in jobTechnologies)
        {
            if (candidateProfile.Contains(tech, StringComparison.OrdinalIgnoreCase))
            {
                matched.Add(tech);
            }
            else
            {
                missing.Add(tech);
            }
        }

        return new JobFitAnalysis(matched, missing);
    }
}

public sealed record JobFitAnalysis(
    IReadOnlyList<string> MatchedSignals,
    IReadOnlyList<string> MissingSignals);
