using DotNetConf.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["RetrievalBackend:BaseUrl"] ?? "http://127.0.0.1:5100/";
    var timeoutSeconds = configuration.GetValue<int?>("RetrievalBackend:TimeoutSeconds") ?? 20;

    if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
    {
        baseUrl += "/";
    }

    return new HttpClient
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };
});

builder.Services.AddSingleton<RetrievalBackendClient>();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SearchJobsTool>();

await builder.Build().RunAsync();