namespace DotNetConf.RagServer.Contracts;

public sealed record RetrievalMatch(
    string JobId,
    string Title,
    string Company,
    string Location,
    string WorkModel,
    string EmploymentType,
    string Seniority,
    string Url,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Tags,
    string Highlight,
    float Score);
