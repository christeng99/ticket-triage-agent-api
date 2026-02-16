using System.ClientModel;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
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

builder.Services.AddScoped<ITicketTriageService, OpenRouterTicketTriageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.UseAuthorization();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.Run();

record CreateTicketRequest(string Title, string Description);
