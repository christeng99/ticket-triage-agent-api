using System.ClientModel;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using OpenAI;
using OpenAI.Chat;
using SupportAgent.Api.AgentPlugins;
using SupportAgent.Api.AI;
using SupportAgent.Api.Data;
using SupportAgent.Api.Domain;
using SupportAgent.Api.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=supportagent.db";
    options.UseSqlite(cs);
});

builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddHostedService<TicketTriageWorker>();

builder.Services.AddSingleton<ChatClient>(_ =>
{
    var apiKey = builder.Configuration["OpenRouter:ApiKey"]
        ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
        ?? throw new InvalidOperationException("Missing OpenRouter API Key");

    var model = builder.Configuration["OpenRouter:Model"]
        ?? throw new InvalidOperationException("Missing OpenRouter model");

    var options = new OpenAIClientOptions
    {
        Endpoint = new Uri("https://openrouter.ai/api/v1")
    };

    return new ChatClient(
        model: model,
        credential: new ApiKeyCredential(apiKey),
        options: options
    );
});

builder.Services.AddScoped<ITicketTriageService, SemanticKernelTicketTriageService>();
builder.Services.AddScoped<KbPlugin>();
builder.Services.AddSingleton<EmbeddingService>();

builder.Services.AddScoped<Kernel>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var apiKey = cfg["OpenRouter:ApiKey"]
        ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
        ?? throw new InvalidOperationException("Missing OpenRouter API key.");

    var model = cfg["OpenRouter:Model"]
        ?? throw new InvalidOperationException("Missing OpenRouter model.");

#pragma warning disable SKEXP0010
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: model,
        apiKey: apiKey,
        endpoint: new Uri("https://openrouter.ai/api/v1")
    );
#pragma warning restore SKEXP0010

    var kernel = kernelBuilder.Build();
    kernel.Plugins.AddFromObject(
        new TicketPlugin(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ILogger<TicketPlugin>>()
        ),
        "ticket"
    );
    kernel.Plugins.AddFromObject(new PolicyPlugin(), "policy");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<KbPlugin>(), "kb");
    kernel.Plugins.AddFromObject(new TelemetryPlugin(sp.GetRequiredService<AppDbContext>()), "telemetry");

    return kernel;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.UseAuthorization();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

using (var scope =  app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var embed = scope.ServiceProvider.GetRequiredService<EmbeddingService>();

    if (!db.KbArticles.Any())
    {
        var articles = new[]
        {
            new
            {
                Title = "Login 401 after signup",
                Body = "Check auth middleware, token issuer, and user role claims. Verify env vars and signing key rotation."
            },
            new
            {
                 Title = "Service Bus timeouts",
                 Body = "Check retry policy, lock duration, and dead-letter queue. Validate connection string and network rules."
            },
            new
            {
                Title = "EF Core migrations",
                Body = "Ensure correct startup project. Use Add-Migration then Update-Database."
            },
            new
            {
                Title = "Possible data breach / unauthorized access",
                Body = "Customer reports seeing another user's data or unauthorized access to their account. Immediately escalate to Security team. Do NOT provide detailed response.Requires human review and incident handling procedure."
            }
        };

        foreach (var a in articles)
        {
            var embedding = await embed.GenerateEmbeddingAsync(a.Title + " " + a.Body); 
            db.KbArticles.Add(new KbArticle
            {
                Title = a.Title,
                Body = a.Body,
                EmbeddingJson = JsonSerializer.Serialize(embedding)
            });
        }
        await db.SaveChangesAsync();
    }
}

app.MapPost("/tickets", async (CreateTicketRequest req, AppDbContext db, Channel<Guid> queue, CancellationToken ct) =>
{
    var ticket = new Ticket
    {
        Title = req.Title.Trim(),
        Description = req.Description.Trim(),
        Status = TicketStatus.Queued
    };

    db.Tickets.Add(ticket);
    await db.SaveChangesAsync(ct);

    await queue.Writer.WriteAsync(ticket.Id, ct);

    return Results.Created($"/tickets/{ticket.Id}", ticket);
});

app.MapGet("/tickets/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
{
    var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
    return ticket is null ? Results.NotFound() : Results.Ok(ticket);
});

app.MapGet("/ticket/{id:guid}/agent-log", async (Guid id, AppDbContext db, CancellationToken ct) =>
{
    var logs = await db.TicketAgentActionLogs
        .Where(l => l.TicketId == id)
        .OrderBy(l => l.CreatedAt)
        .ToListAsync(ct);

    return Results.Ok(logs);
});

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.Run();

record CreateTicketRequest(string Title, string Description);
