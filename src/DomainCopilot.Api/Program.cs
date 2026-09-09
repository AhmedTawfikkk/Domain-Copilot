using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Ingestion;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Providers;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Configuration
// ============================================================

var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "POSTGRES_CONNECTION_STRING is required. Copy .env.example to .env and set the value.");

// ============================================================
// Services — Web / API infrastructure
// ============================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// ============================================================
// Services — Database
// ============================================================

builder.Services.AddDbContext<DomainCopilotDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseVector()));

// ============================================================
// Services — LLM Providers (Day 4)
// ============================================================

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

// ============================================================
// Services — Ingestion Pipeline (Day 5)
// ============================================================

builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ITextExtractor, DocxTextExtractor>();
builder.Services.AddScoped<ITextExtractor, PlainTextExtractor>();
builder.Services.AddScoped<IDocumentTextExtractor, CompositeTextExtractor>();
builder.Services.AddScoped<ITextCleaner, LegalTextCleaner>();
builder.Services.AddScoped<IClauseChunker, ClauseAwareChunker>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// ============================================================
// App pipeline
// ============================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

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