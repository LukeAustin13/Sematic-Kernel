using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SematicKernelWpf.ViewModel
{
    /// <summary>
    /// WPF App for Semantic Kernel OpenAI Chat Completion
    /// </summary>
    public class MainWindowViewModel
    {
        //Keneral properties 
        private IConfiguration configuration;
        private string? apiKey;
        private readonly string model = "gpt-4.1-mini";
        ChatHistory history;

        //WPF properties - still TODO
        public ViewModelCommand SubmitCommand { get; set; }

        public MainWindowViewModel()
        {
            CreateConnection();
        }

        /// <summary>
        /// Creates a connection to OpenAI client
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void CreateConnection()
        {
            //Getting API Key from User Secrets
            configuration = new ConfigurationBuilder()
                .AddUserSecrets<MainWindowViewModel>()
                .Build();

            apiKey = configuration["OpenAI:ApiKey"]; //When creating Secrets.Json - make sure it follows the format otherwise it throws "System.IO.InvalidDataException"

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI key is invalid");
            }

            //Creating Kernel
            var kernelBuild = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(model, apiKey)
                .Build();

            var openAIChat = kernelBuild.GetRequiredService<IChatCompletionService>();
            history = new ChatHistory();
            //Adding a system message gives context to the OpenAI client, telling it how to act/think.
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker."); 
            
        }

        public void submit()
        {

        }
    }
}
