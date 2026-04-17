using DotNetConf.Knowledge.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using OpenAI.Embeddings;
using Azure.AI.OpenAI;
using System.ClientModel;

namespace DotNetConf.Seeder.Embedding;

public sealed record EmbeddingSettings(
    EmbeddingProvider Provider,
    string Model,
    string? ApiKey,
    int Dimensions,
    string? Endpoint = null,
    string? Deployment = null)
{
    public string Mode => Provider switch
    {
        EmbeddingProvider.OpenAI => $"openai:{Model}",
        EmbeddingProvider.AzureOpenAI => $"azure-openai:{Model}",
        _ => throw new InvalidOperationException($"Unsupported embedding provider {Provider}.")
    };

    public static EmbeddingSettings Load(IConfiguration configuration)
    {
        var model = configuration["Embedding:Model"]
            ?? configuration["OPENAI_EMBEDDING_MODEL"]
            ?? configuration["AzureOpenAI:ModelName"]
            ?? "text-embedding-3-small";

        var apiKey = configuration["Embedding:ApiKey"]
            ?? configuration["OPENAI_API_KEY"];
        var azureApiKey = configuration["AzureOpenAI:ApiKey"];
        var azureEndpoint = configuration["AzureOpenAI:Endpoint"];
        var azureDeployment = configuration["AzureOpenAI:Deployment"];

        var rawDimensions = configuration["Embedding:Dimensions"]
            ?? configuration["OPENAI_EMBEDDING_DIMENSIONS"];

        var dimensions = KnowledgeConstants.DefaultEmbeddingDimensions;
        if (!string.IsNullOrWhiteSpace(rawDimensions) && int.TryParse(rawDimensions, out var parsedDimensions))
        {
            dimensions = parsedDimensions;
        }

        if (dimensions != KnowledgeConstants.DefaultEmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimensions must remain {KnowledgeConstants.DefaultEmbeddingDimensions} for the SQLite vector schema.");
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return new EmbeddingSettings(EmbeddingProvider.OpenAI, model, apiKey, dimensions);
        }

        if (!string.IsNullOrWhiteSpace(azureApiKey) &&
            !string.IsNullOrWhiteSpace(azureEndpoint) &&
            !string.IsNullOrWhiteSpace(azureDeployment))
        {
            return new EmbeddingSettings(
                EmbeddingProvider.AzureOpenAI,
                model,
                azureApiKey,
                dimensions,
                Endpoint: azureEndpoint,
                Deployment: azureDeployment);
        }

        throw new InvalidOperationException(
            "OpenAI embeddings are required. Configure Embedding:ApiKey or AzureOpenAI credentials before running the seeder.");
    }

    public IEmbeddingGenerator<string, Embedding<float>> CreateGenerator() => Provider switch
    {
        EmbeddingProvider.OpenAI => new EmbeddingClient(
                Model,
                ApiKey ?? throw new InvalidOperationException("Embedding:ApiKey is required for OpenAI embeddings."))
            .AsIEmbeddingGenerator(defaultModelDimensions: Dimensions),
        EmbeddingProvider.AzureOpenAI => new AzureOpenAIClient(
                new Uri(Endpoint ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.")),
                new ApiKeyCredential(ApiKey ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required.")))
            .GetEmbeddingClient(Deployment ?? throw new InvalidOperationException("AzureOpenAI:Deployment is required."))
            .AsIEmbeddingGenerator(defaultModelDimensions: Dimensions),
        _ => throw new InvalidOperationException($"Unsupported embedding provider {Provider}.")
    };
}

public enum EmbeddingProvider
{
    OpenAI,
    AzureOpenAI
}
