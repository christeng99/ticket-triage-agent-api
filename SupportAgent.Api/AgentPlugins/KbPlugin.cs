using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.SemanticKernel;
using SupportAgent.Api.AI;
using SupportAgent.Api.Data;

namespace SupportAgent.Api.AgentPlugins
{
    public class KbPlugin
    {
        private readonly ILogger<KbPlugin> _logger;
        private readonly AppDbContext _db;

        public KbPlugin(ILogger<KbPlugin> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        [KernelFunction("search_kb")]
        public async Task<string> SearchKb(string query)
        {
            _logger.LogInformation("KB search query: {Query}", query);
            var embedService = _db.GetService<EmbeddingService>();
            var queryEmbedding = await embedService.GenerateEmbeddingAsync(query);

            var articles = await _db.KbArticles.ToListAsync();

            var scored = articles
                .Select(a =>
                {
                    var vector = JsonSerializer.Deserialize<float[]>(a.EmbeddingJson)!;
                    var score = VectorMath.CosineSimilarity(queryEmbedding, vector);
                    return new
                    {
                        a.Title,
                        a.Body,
                        Score = score
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(3);

            _logger.LogInformation("Found top KB articles: {Results}", JsonSerializer.Serialize(scored));



            return JsonSerializer.Serialize(new { results = scored });
        }
    }
}
