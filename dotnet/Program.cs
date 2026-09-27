using KirtisMcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string BaseUrl = "https://kirtis.info/api/";

void AddKirtisServices(IServiceCollection services, CacheSettings cacheSettings)
{
    services.AddHttpClient("kirtis", client =>
    {
        client.BaseAddress = new Uri(BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Accept-Language", "lt-LT,lt;q=0.9,en;q=0.8");
    });
    services.AddSingleton(cacheSettings);
    services.AddSingleton<KirtisCacheService>();
    services.AddHostedService(sp => sp.GetRequiredService<KirtisCacheService>());
}

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("kirtis-mcp — Lithuanian stress and grammatical info from kirtis.info");
    Console.WriteLine();
    Console.WriteLine("USAGE");
    Console.WriteLine("  KirtisMcp                     MCP stdio server (default, for Claude Code)");
    Console.WriteLine("  KirtisMcp --sse [--port N]    MCP SSE + REST server (default port: 8020)");
    Console.WriteLine("  KirtisMcp --help              Show this help");
    Console.WriteLine();
    Console.WriteLine("OPTIONS");
    Console.WriteLine("  --sse             Run as HTTP server instead of stdio");
    Console.WriteLine("  --port N          Port for SSE mode (default: 8020)");
    Console.WriteLine("  --no-cache        Skip local cache, always fetch from server");
    Console.WriteLine("  --cache-update    Write server responses back to local cache");
    Console.WriteLine();
    Console.WriteLine("CACHE");
    Console.WriteLine("  By default the local cache (Kirtis/ folder) is read-only.");
    Console.WriteLine("  --cache-update appends new lookups to Kirtis/kirtis_updates.json.");
    Console.WriteLine("  Set KIRTIS_CACHE_PATH env var to override cache directory.");
    Console.WriteLine();
    Console.WriteLine("MCP TOOLS");
    Console.WriteLine("  get_word_info  Full two-phase lookup — preferred entry point");
    Console.WriteLine("  lookup_words   Prefix-search for related word forms");
    Console.WriteLine("  get_stress     Stress marks and grammatical tags for one word");
    Console.WriteLine();
    Console.WriteLine("SSE MODE ENDPOINTS");
    Console.WriteLine("  /mcp                        MCP over SSE");
    Console.WriteLine("  POST /lookup_words          { \"word\": \"...\" }");
    Console.WriteLine("  POST /get_stress            { \"word\": \"...\" }");
    Console.WriteLine("  POST /get_word_info         { \"word\": \"...\" }");
    return;
}

var cacheSettings = new CacheSettings(
    ReadEnabled: !args.Contains("--no-cache"),
    WriteEnabled: args.Contains("--cache-update")
);

if (args.Contains("--sse"))
{
    var port = args.Contains("--port")
        ? int.Parse(args[Array.IndexOf(args, "--port") + 1])
        : 8020;

    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

    AddKirtisServices(builder.Services, cacheSettings);
    builder.Services.AddScoped<KirtisTools>();
    builder.Services.AddMcpServer().WithHttpTransport().WithTools<KirtisTools>();

    var app = builder.Build();

    // MCP SSE endpoint (for Claude Code / Open WebUI MCP integration)
    app.MapMcp("/mcp");

    // REST proxy endpoints (for Open WebUI OpenAPI integration)
    app.MapPost("/lookup_words", async (WordRequest req, KirtisTools tools) =>
        await tools.LookupWords(req.Word));

    app.MapPost("/get_stress", async (WordRequest req, KirtisTools tools) =>
        await tools.GetStress(req.Word));

    app.MapPost("/get_word_info", async (WordRequest req, KirtisTools tools) =>
        await tools.GetWordInfo(req.Word));

    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    AddKirtisServices(builder.Services, cacheSettings);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<KirtisTools>();
    await builder.Build().RunAsync();
}

record WordRequest(string Word);
