using DotNetConf.RagServer.Contracts;
using DotNetConf.RagServer.Options;
using DotNetConf.RagServer.Services;
using DotNetConf.Knowledge.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI.Embeddings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RetrievalOptions>(builder.Configuration.GetSection(RetrievalOptions.SectionName));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));

var vectorConnectionString = ResolveVectorConnectionString(builder);

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<RetrievalOptions>>().Value;

    return (VectorStoreCollection<string, JobChunkDocument>)new SqliteCollection<string, JobChunkDocument>(
        vectorConnectionString,
        options.CollectionName);
});

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;

    // dotnet user-secrets set "Embedding:ApiKey" "YOUR_KEY_HERE" --project src/rag-server/DotNetConf.RagServer.csproj
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "Embedding:ApiKey is required. Set it in appsettings.json or user secrets.");
    }

    return new EmbeddingClient(options.Model, options.ApiKey)
        .AsIEmbeddingGenerator();
});

builder.Services.AddSingleton<JobFilterMatcher>();
builder.Services.AddSingleton<RetrievalHealthService>();
builder.Services.AddSingleton<RetrievalService>();

var app = builder.Build();

await app.Services.GetRequiredService<RetrievalHealthService>().EnsureStoreReadyAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/health", async (RetrievalHealthService healthService, CancellationToken cancellationToken) =>
{
    var status = await healthService.CheckAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/retrieve", async (
    RetrievalRequest request,
    RetrievalService retrievalService,
    CancellationToken cancellationToken) =>
{
    var validationErrors = RetrievalRequestValidator.Validate(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var response = await retrievalService.RetrieveAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.Run();

static string ResolveVectorConnectionString(WebApplicationBuilder builder)
{
    var configuredConnectionString = builder.Configuration.GetConnectionString("VectorDb");
    var connectionStringBuilder = string.IsNullOrWhiteSpace(configuredConnectionString)
        ? new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine("..", "..", "artifacts", "devbg-jobs.db")
        }
        : new SqliteConnectionStringBuilder(configuredConnectionString);

    if (!Path.IsPathRooted(connectionStringBuilder.DataSource))
    {
        connectionStringBuilder.DataSource = Path.GetFullPath(
            Path.Combine(builder.Environment.ContentRootPath, connectionStringBuilder.DataSource));
    }

    return connectionStringBuilder.ToString();
}
