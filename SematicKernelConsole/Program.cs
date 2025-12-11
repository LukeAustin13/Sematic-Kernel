using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var config = new ConfigurationBuilder()
             .AddUserSecrets<Program>()
             .Build();

var apiKey = config["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("OpenAI key is invalid");


}

const string model = "gpt-4.1-mini";

var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(model, apiKey);

Kernel kernel = builder.Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();
history.AddSystemMessage("You are a concise assistant that provides brief answers.");

Console.WriteLine(string.Format("This is talking to OpenAI using {0} and using .Net Semantic Kernel", model));

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("User: ");
    Console.ResetColor();

    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    history.AddUserMessage(userInput);

    var response = await chat.GetChatMessageContentAsync(history);

    history.Add(response);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(string.Format("\nAI: {0}\n", response.Content));

}
