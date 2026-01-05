using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using SematicKernelWpf.Model;

namespace SematicKernelWpf.ViewModel
{
    /// <summary>
    /// WPF App for Semantic Kernel OpenAI Chat Completion & Image Generation
    /// Some errors have been suppressed as they are flagged as experimental features in Semantic Kernel
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        //Keneral properties 
        private IConfiguration configuration;
        private string? apiKey;
        private readonly string chatModel = "gpt-4.1-mini";
        ChatHistory? history;
        private IChatCompletionService? chatService;

#pragma warning disable SKEXP0001 
        private ITextToImageService? imageService;
#pragma warning restore SKEXP0001

        //Commands
        public AsyncViewModelCommand SubmitCommand { get; set; }
        public ViewModelCommand ClearCommand { get; set; }
        public ViewModelCommand ReconnectCommand { get; set; }
        public ViewModelCommand ExitCommand { get; set; }

        public AsyncViewModelCommand GenerateImageCommand { get; }
        public ViewModelCommand CancelImageCommand { get; }
        public ViewModelCommand SaveImageCommand { get; }

        //Properties
        #region Chat Completion Properties
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
        #endregion

        #region Image Generation Properties
        private string _imagePrompt = "";
        public string ImagePrompt
        {
            get => _imagePrompt;
            set { _imagePrompt = value; OnPropertyChanged(); }
        }

        private BitmapImage? _generatedImage;
        public BitmapImage? GeneratedImage
        {
            get => _generatedImage;
            set { _generatedImage = value; OnPropertyChanged(); }
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set { _isGenerating = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource? _imgCts;
        private byte[]? _lastImageBytes;
        #endregion


        public MainWindowViewModel()
        {
            //Set up connection to OpenAI
            CreateConnection();
            ValidateConnection();

            //Set up WPF Commands
            SubmitCommand = new AsyncViewModelCommand(SubmitAsync); //this doesnt work? figure out why! (When the button is clicked, nothing actually happens)
            ClearCommand = new ViewModelCommand(Clear);
            ReconnectCommand = new ViewModelCommand(ResetConnection);
            ExitCommand = new ViewModelCommand(() => Application.Current.Shutdown());

            GenerateImageCommand = new AsyncViewModelCommand(GenerateImageAsync);
            CancelImageCommand = new ViewModelCommand(CancelImage) { IsEnabled = false };
            SaveImageCommand = new ViewModelCommand(SaveImage) { IsEnabled = false };
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

            ////Creating Kernel - old way, realised when wanting more than one type of service (chat and image generation) can just add both to same kernel
            //var kernelBuild = Kernel.CreateBuilder()
            //    .AddOpenAIChatCompletion(chatModel, apiKey)
            //    .Build();


#pragma warning disable SKEXP0010
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: chatModel, apiKey: apiKey)
                .AddOpenAITextToImage(apiKey: apiKey, modelId: "gpt-image-1")
                .Build();
            //To use OpenAITextToImage - need to have the correct modelId and verification
#pragma warning restore SKEXP0010

            chatService = kernel.GetRequiredService<IChatCompletionService>();
            history = new ChatHistory();
            //Adding a system message gives context to the OpenAI client, telling it how to act/think.
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");


#pragma warning disable SKEXP0001 
            imageService = kernel.GetRequiredService<ITextToImageService>();
#pragma warning restore SKEXP0001 


        }

        public async Task SubmitAsync()
        {

            if (!ValidateConnection())
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

        private void Clear()
        {
            history.Clear();
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");
            UserMessage = string.Empty;
            WholeChat = string.Empty;
        }

        private void ResetConnection()
        {
            CreateConnection();
            ValidateConnection();
        }

        private bool ValidateConnection() //Needs to validate both chat and image services eventually, also history -- but could be separate methods?
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

        private async Task GenerateImageAsync() //TODO: get verified on OpenAI to use image generation!! Cant progress until then.
        {
            IsGenerating = true;
            _imgCts = new CancellationTokenSource();

            try
            {
                string imageGenerationResult = await imageService.GenerateImageAsync(
                    description: ImagePrompt,
                    width: 1024,
                    height: 1024,
                    cancellationToken: _imgCts.Token);

                _lastImageBytes = await ImageDecoder.ImageStringToBytesAsync(imageGenerationResult, _imgCts.Token);
                GeneratedImage = ImageDecoder.BytesToBitmapImage(_lastImageBytes);

                SaveImageCommand.RaiseCanExecuteChanged();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during image generation: {ex.Message}");
            }
            finally
            {
                IsGenerating = false;
                CancelImageCommand.RaiseCanExecuteChanged();
            }

        }

        private void CancelImage()
        {
            _imgCts?.Cancel();
        }
        private void SaveImage()
        {

            if (_lastImageBytes == null || _lastImageBytes.Length == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = "generated.png"
            };

            if (dlg.ShowDialog() == true)
                System.IO.File.WriteAllBytes(dlg.FileName, _lastImageBytes);

        }
    }
}
