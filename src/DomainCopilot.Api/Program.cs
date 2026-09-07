using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Register HttpClient factories for each provider
builder.Services.AddHttpClient<OllamaProvider>();
builder.Services.AddScoped<OllamaProvider>();

builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("groq");
    var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? string.Empty;
    return new GroqProvider(httpClient, apiKey);
});

// The active ILlmProvider is selected here based on config (LLM_PROVIDER env var)
builder.Services.AddScoped<ILlmProvider>(sp =>
{
    var providerName = builder.Configuration["LLM_PROVIDER"] ?? "ollama";
    var ollama = sp.GetRequiredService<OllamaProvider>();
    var groq = sp.GetRequiredService<GroqProvider>();
    var logger = sp.GetRequiredService<ILogger<FallbackLlmProvider>>();

    return providerName.ToLowerInvariant() switch
    {
        "groq" => new FallbackLlmProvider(groq, ollama, logger),
        _ => new FallbackLlmProvider(ollama, groq, logger)
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Temporary test endpoint — will be replaced by the real /api/query endpoint on Day 7
app.MapPost("/test/complete", async (ILlmProvider provider, TestCompleteRequest request) =>
{
    var result = await provider.CompleteAsync(
        "You are a helpful assistant.",
        request.Prompt,
        CancellationToken.None);
    return Results.Ok(new { response = result, providerType = provider.GetType().Name });
})
.WithName("TestComplete");

app.Run();

record TestCompleteRequest(string Prompt);