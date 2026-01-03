using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace SematicKernelWpf.ViewModel
{
    /// <summary>
    /// WPF App for Semantic Kernel OpenAI Chat Completion
    /// </summary>
    public class MainWindowViewModel: INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        //Keneral properties 
        private IConfiguration configuration;
        private string? apiKey;
        private readonly string model = "gpt-4.1-mini";
        ChatHistory? history;
        private IChatCompletionService? chatService;

        //Commands
        public AsyncViewModelCommand SubmitCommand { get; set; }
        public ViewModelCommand ClearCommand { get; set; }
        public ViewModelCommand ReconnectCommand { get; set; }
        public ViewModelCommand ExitCommand { get; set; }

        //properties for binding
        private string _userMessage = "";
        public string UserMessage
        {
            get => _userMessage;
            set { _userMessage = value; OnPropertyChanged(); }
        }

        private string _wholeChat = "";
        public string WholeChat
        {
            get => _wholeChat;
            set { _wholeChat = value; OnPropertyChanged(); }
        }


        public MainWindowViewModel()
        {
            //Set up connection to OpenAI
            CreateConnection();
            validateConnection();

            //Set up WPF Commands
            SubmitCommand = new AsyncViewModelCommand(SubmitAsync); //this doesnt work? figure out why! (When the button is clicked, nothing actually happens)
            ClearCommand = new ViewModelCommand(clear);
            ReconnectCommand = new ViewModelCommand(resetConnection);
            ExitCommand = new ViewModelCommand(() => Application.Current.Shutdown());
        }

        /// <summary>
        /// Creates a connection to OpenAI client
        /// </summary>
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

            chatService = kernelBuild.GetRequiredService<IChatCompletionService>();
            history = new ChatHistory();
            //Adding a system message gives context to the OpenAI client, telling it how to act/think.
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");
        }

        public async Task SubmitAsync()
        {
            
            if (!validateConnection())
            {
                MessageBox.Show("Connection is not valid.\nEither chat history is null or the chat service was unable to establish a connection.");
                return;
            }
            
            var userInput = UserMessage?.Trim();
            if (string.IsNullOrWhiteSpace(userInput))
            {
                MessageBox.Show("Please enter a message.");
                return;
            }

            try
            {
                history.AddUserMessage(userInput);

                var response = await chatService.GetChatMessageContentAsync(history);

                history.Add(response);

                WholeChat += $"User: {userInput}\nAI: {response.Content}\n\n";
                UserMessage = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }            
        }

        private void clear()
        {
            history.Clear();
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");
        }

        private void resetConnection()
        {
            CreateConnection();
            validateConnection();
        }

        private bool validateConnection()
        {
            if (chatService == null)
            {
                throw new InvalidOperationException("Connection to OpenAI is not established.");
                return false;
            }

            if (history == null)
            {
                throw new InvalidOperationException("Chat history is not initialized.");
                return false;
            }

            return true;
        }
    }
}
