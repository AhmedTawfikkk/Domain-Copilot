using DomainCopilot.Application.Documents.Answering;
using DomainCopilot.Application.Documents.Ingestion;
using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Application.Providers;
using DomainCopilot.Api.Security;
using DomainCopilot.Infrastructure.Exports;
using DomainCopilot.Infrastructure.Ingestion;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Repositories;
using DomainCopilot.Infrastructure.Providers;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var environmentFilePath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..",
    "..",
    "..",
    ".env"));

if (File.Exists(environmentFilePath))
{
    Env.Load(environmentFilePath);
}

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

var apiSecurityOptions = new ApiSecurityOptions();

builder.Configuration
    .GetSection("ApiSecurity")
    .Bind(apiSecurityOptions);

apiSecurityOptions.Validate();
builder.Services.AddSingleton(apiSecurityOptions);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("expensive-operations", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.AddFixedWindowLimiter("state-changing", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.AddFixedWindowLimiter("retrieval", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

// ============================================================
// Services — Database
// ============================================================

builder.Services.AddDbContext<DomainCopilotDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseVector()));

// ============================================================
// Services — LLM Providers (Day 4)
// ============================================================

builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddScoped<OllamaProvider>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new OllamaProvider(httpClientFactory.CreateClient("ollama"));
});


//groq
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("groq");
    var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? string.Empty;
    return new GroqProvider(httpClient, apiKey);
});


// gemini
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("gemini");
    var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? string.Empty;
    return new GeminiProvider(httpClient, apiKey);
});

// The active ILlmProvider is selected here based on config (LLM_PROVIDER env var)
//builder.Services.AddScoped<ILlmProvider>(sp =>
//{
//    var providerName = builder.Configuration["LLM_PROVIDER"] ?? "ollama";
//    var ollama = sp.GetRequiredService<OllamaProvider>();
//    var groq = sp.GetRequiredService<GroqProvider>();
//    var logger = sp.GetRequiredService<ILogger<FallbackLlmProvider>>();

//    return providerName.ToLowerInvariant() switch
//    {
//        "groq" => new FallbackLlmProvider(groq, ollama, logger),
//        _ => new FallbackLlmProvider(ollama, groq, logger)
//    };
//});

builder.Services.AddScoped<ILlmProvider>(sp =>
{
    var providerName = builder.Configuration["LLM_PROVIDER"] ?? "gemini";
    var ollama = sp.GetRequiredService<OllamaProvider>();
    var gemini = sp.GetRequiredService<GeminiProvider>();
    var logger = sp.GetRequiredService<ILogger<FallbackLlmProvider>>();

    return providerName.ToLowerInvariant() switch
    {
        "ollama" => new FallbackLlmProvider(ollama, gemini, logger), 
        _ => new FallbackLlmProvider(gemini, ollama, logger) // default: gemini primary
    };
});

// ============================================================
// Services — Ingestion Pipeline (Day 5 + Day 10 OCR)
// ============================================================

var ingestionPolicy = new DocumentIngestionPolicy();

builder.Configuration
    .GetSection("DocumentIngestion")
    .Bind(ingestionPolicy);

ingestionPolicy.Validate();

var pdfOcrOptions = new PdfOcrOptions();

builder.Configuration
    .GetSection("PdfOcr")
    .Bind(pdfOcrOptions);

pdfOcrOptions.Validate();

builder.Services.AddSingleton(ingestionPolicy);
builder.Services.AddSingleton(pdfOcrOptions);

builder.Services.AddSingleton<IPdfOcrService,
    TesseractPdfOcrService>();

builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ITextExtractor, DocxTextExtractor>();
builder.Services.AddScoped<ITextExtractor, PlainTextExtractor>();

builder.Services.AddScoped<IDocumentTextExtractor,
    CompositeTextExtractor>();

builder.Services.AddScoped<ITextCleaner,
    LegalTextCleaner>();

builder.Services.AddScoped<IClauseChunker,
    ClauseAwareChunker>();

builder.Services.AddScoped<IDocumentIngestionService,
    DocumentIngestionService>();


// ============================================================
// Services — Embeddings and Retrieval (Day 6)
// ============================================================

builder.Services.AddScoped<IChunkRetrievalRepository,
    ChunkRetrievalRepository>();

builder.Services.AddScoped<IEmbeddingIndexingService,
    EmbeddingIndexingService>();

builder.Services.AddScoped<IChunkRetrievalService,
    ChunkRetrievalService>();

// ============================================================
// Services — Grounded Answers and Citations (Day 7)
// ============================================================

builder.Services.AddSingleton<IGroundedAnswerPromptTemplate,
    GroundedAnswerPromptTemplate>();

builder.Services.AddScoped<IGroundedAnswerService,
    GroundedAnswerService>();

// ============================================================
// Services — Clause Extractor, Risk Assessor, and Orchestration (Day 8)
// ============================================================

var reviewPolicy = new ReviewExecutionPolicy();

builder.Configuration
    .GetSection("LegalReview")
    .Bind(reviewPolicy);

reviewPolicy.Validate();

builder.Services.AddSingleton(reviewPolicy);

builder.Services.AddScoped<IDocumentReviewRepository,
    DocumentReviewRepository>();

builder.Services.AddSingleton<IClauseExtractionPromptTemplate,
    ClauseExtractionPromptTemplate>();

builder.Services.AddScoped<IClauseExtractorAgent,
    ClauseExtractorAgent>();

builder.Services.AddSingleton<IContractReviewPlaybook,
    LegalContractPlaybook>();

builder.Services.AddScoped<IRiskAssessorAgent,
    RiskAssessorAgent>();

builder.Services.AddScoped<ILegalReviewOrchestrator,
    LegalReviewOrchestrator>();

// ============================================================
// Services — Memo Drafting and Persistence (Day 9)
// ============================================================

builder.Services.AddSingleton<IMemoDraftPromptTemplate,
    MemoDraftPromptTemplate>();

builder.Services.AddScoped<IMemoDrafterAgent,
    MemoDrafterAgent>();

builder.Services.AddScoped<IReviewMemoRepository,
    ReviewMemoRepository>();
builder.Services.AddScoped<IMemoApprovalService,
    MemoApprovalService>();

builder.Services.AddSingleton<IReviewMemoDocxRenderer,
    OpenXmlReviewMemoDocxRenderer>();

builder.Services.AddScoped<IReviewMemoExportService,
    ReviewMemoExportService>();

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
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseRateLimiter();
app.MapControllers();


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
