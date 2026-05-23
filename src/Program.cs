using JeminiLateUse.Config;
using JeminiLateUse.Server;

var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Error: GEMINI_API_KEY environment variable is not set");
    Environment.Exit(1);
}

var model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-1.5-flash";
var port = int.TryParse(Environment.GetEnvironmentVariable("SERVER_PORT"), out var p) ? p : 8080;

var config = new GeminiConfig
{
    ApiKey = apiKey,
    Model = model
};

var server = new ContinueIntegrationServer(config, port);
await server.StartAsync();
