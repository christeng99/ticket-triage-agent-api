using System.ClientModel;
using OpenAI;
using OpenAI.Embeddings;

namespace SupportAgent.Api.AI
{
    public class EmbeddingService
    {
        private readonly EmbeddingClient _embeddingClient;

        public EmbeddingService(IConfiguration config)
        {
            var apiKey =
                config["OpenRouter:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                ?? throw new InvalidOperationException("Missing OpenRouter API key.");

            var model = config["OpenRouter:EmbeddingModel"]
                ?? "openai/text-embedding-3-small";

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://openrouter.ai/api/v1")
            };

            _embeddingClient = new EmbeddingClient(
                model: model,
                credential: new ApiKeyCredential(apiKey),
                options: options
            );
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats().ToArray();
        }
    }
}
