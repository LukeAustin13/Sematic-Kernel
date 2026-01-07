using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;
using SematicKernelWpf.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SematicKernelWpf.ViewModel
{
    /// <summary>
    /// WPF App for Semantic Kernel OpenAI Chat Completion & Image Generation
    /// Some errors have been suppressed as they are flagged as experimental features in Semantic Kernel
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {


        #region Kernel and related fields
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

        private IChatCompletionService studyChatService;
        private ChatHistory studyChatHistory;

        #endregion

        //Commands
        #region Chat Completion Commands
        public AsyncViewModelCommand SubmitCommand { get; set; }
        public ViewModelCommand ClearCommand { get; set; }
        public ViewModelCommand ReconnectCommand { get; set; }
        public ViewModelCommand ExitCommand { get; set; }
        #endregion

        #region Image Generation Commands
        public AsyncViewModelCommand GenerateImageCommand { get; }
        public ViewModelCommand CancelImageCommand { get; }
        public ViewModelCommand SaveImageCommand { get; }
        #endregion

        #region Study Plugin Commands
        public AsyncViewModelCommand RefreshDecksCommand { get; }
        public AsyncViewModelCommand GenerateFlashcardsCommand { get; }
        public AsyncViewModelCommand NextCardCommand { get; }

        public ViewModelCommand CreateDeckCommand { get; }
        public ViewModelCommand ShowAnswerCommand { get; }
        public ViewModelCommand RateWrongCommand { get; }
        public ViewModelCommand RateHardCommand { get; }
        public ViewModelCommand RateGoodCommand { get; }   
        public ViewModelCommand RateEasyCommand { get; }

        #endregion

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

        #region Study Plugin Properties


        public ObservableCollection<string> StudyDecks { get; set; } = new ObservableCollection<string>();
        public string SelectedDeck { get; set; } = string.Empty;
        public string StudyNotes { get; set; } = string.Empty;
        public string StudyStatus { get; set; } = string.Empty;
        public string CurrentCardFront { get; set; } = string.Empty;
        public string CurrentCardBack { get; set; } = string.Empty;

        private readonly StudyStorage _studyStorage = new StudyStorage();
        private StudyPlugin _studyPlugin;
        private Flashcard? _currentCard;

        private string _selectedStudyDeck = string.Empty;
        public string SelectedStudyDeck
        {
            get => _selectedStudyDeck;
            set
            {
                if (_selectedStudyDeck == value) return;
                _selectedStudyDeck = value;
                OnPropertyChanged();

                CreateDeckCommand?.RaiseCanExecuteChanged();
                GenerateFlashcardsCommand?.RaiseCanExecuteChanged();
                NextCardCommand?.RaiseCanExecuteChanged();
            }
        }

        #endregion


        public MainWindowViewModel()
        {
            //Set up connection to OpenAI
            CreateConnection();
            ValidateConnection();

            //Set up WPF Commands

            //Chat Completion Commands
            SubmitCommand = new AsyncViewModelCommand(SubmitAsync);
            ClearCommand = new ViewModelCommand(Clear);
            ReconnectCommand = new ViewModelCommand(ResetConnection);
            ExitCommand = new ViewModelCommand(() => Application.Current.Shutdown());

            //Image Generation Commands
            GenerateImageCommand = new AsyncViewModelCommand(GenerateImageAsync);
            CancelImageCommand = new ViewModelCommand(CancelImage) { IsEnabled = false };
            SaveImageCommand = new ViewModelCommand(SaveImage) { IsEnabled = false };

            //Study Plugin Commands
            RefreshDecksCommand = new AsyncViewModelCommand(RefreshDecksAsync);
            GenerateFlashcardsCommand = new AsyncViewModelCommand(GenerateFlashcardsAsync);
            NextCardCommand = new AsyncViewModelCommand(NextCardAsync);

            CreateDeckCommand = new ViewModelCommand(CreateDeck);
            ShowAnswerCommand = new ViewModelCommand(ShowAnswer);

            RateWrongCommand = new ViewModelCommand(() => RateCard(1));
            RateHardCommand = new ViewModelCommand(() => RateCard(2));
            RateGoodCommand = new ViewModelCommand(() => RateCard(3));
            RateEasyCommand = new ViewModelCommand(() => RateCard(4));

            _studyPlugin = new StudyPlugin(_studyStorage);
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

            #region Main Kernel Creation
            ////Creating Kernel - old way, realised when wanting more than one type of service (chat and image generation) can just add both to same kernel
            //var kernelBuild = Kernel.CreateBuilder()
            //    .AddOpenAIChatCompletion(chatModel, apiKey)
            //    .Build();


            //Creating Kernel - new way to add multiple services
            //var kernel = Kernel.CreateBuilder()
            //    .AddOpenAIChatCompletion(modelId: chatModel, apiKey: apiKey)
            //    .AddOpenAITextToImage(apiKey: apiKey, modelId: "gpt-image-1")
            //    .Build();


            //This way allows for adding plugins too
            var kernelBuilder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: chatModel, apiKey: apiKey);

#pragma warning disable SKEXP0010
            //To use OpenAITextToImage - need to have the correct modelId and verification
            kernelBuilder.AddOpenAITextToImage(apiKey: apiKey, modelId: "gpt-image-1");
#pragma warning restore SKEXP0010

            var kernel = kernelBuilder.Build();
            chatService = kernel.GetRequiredService<IChatCompletionService>();
            history = new ChatHistory();
            //Adding a system message gives context to the OpenAI client, telling it how to act/think.
            history.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");


#pragma warning disable SKEXP0001 
            imageService = kernel.GetRequiredService<ITextToImageService>();
#pragma warning restore SKEXP0001
            #endregion


            #region Study Plugin Setup
            var studyBuilder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: chatModel, apiKey: apiKey);

            studyBuilder.Plugins.AddFromObject(new StudyPlugin(_studyStorage), "Study");

            var studyKernel = studyBuilder.Build();

            studyChatService = studyKernel.GetRequiredService<IChatCompletionService>();
            studyChatHistory = new ChatHistory();
            studyChatHistory.AddSystemMessage("You are a study helper. You will keep your answers short and understandable. Use the given tools when they are helpful.");

            #endregion
        }

        private void ResetConnection()
        {
            CreateConnection();
            ValidateConnection();
        }

        #region Chat Completion Methods
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
            history?.Clear();
            history?.AddSystemMessage("You are a chatty assistant that provides detailed answers; Clearly explains themselves; Is a logical thinker.");
            UserMessage = string.Empty;
            WholeChat = string.Empty;
        }

        #endregion

        #region Image Generation Methods
        private bool ValidateConnection() //Needs to validate both chat and image services eventually, also history -- but could be separate methods?
        {
            if (chatService == null)
            {
                throw new InvalidOperationException("Connection to OpenAI is not established.");
                
            }

            if (history == null)
            {
                throw new InvalidOperationException("Chat history is not initialized.");
                
            }

            return true;
        }

        private async Task GenerateImageAsync() //TODO: get verified on OpenAI to use image generation!! Cant progress until then.
        {
            IsGenerating = true;
            _imgCts = new CancellationTokenSource();

            try
            {

                if (imageService == null)
                {
                    MessageBox.Show("Image service is not initialized.");
                    return;
                }

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

        //TODO: TEST
        private void CancelImage()
        {
            _imgCts?.Cancel();
        }

        //TODO: TEST
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
        #endregion

        #region Study Plugin Methods 
        //TODO: TEST 

        private async Task RefreshDecksAsync()
        {
            try
            {
                StudyStatus = "Refreshing...";
                OnPropertyChanged(nameof(StudyStatus));

                var decks = _studyStorage.ListDecks();

                StudyDecks.Clear();
                foreach (var d in decks.OrderBy(x => x))
                    StudyDecks.Add(d);

                if (string.IsNullOrWhiteSpace(SelectedStudyDeck) && StudyDecks.Count > 0)
                    SelectedStudyDeck = StudyDecks[0];

                StudyStatus = $"Found {StudyDecks.Count} deck(s).";
                OnPropertyChanged(nameof(SelectedStudyDeck));
                OnPropertyChanged(nameof(StudyStatus));
            }
            catch (Exception ex)
            {
                StudyStatus = $"Refresh failed: {ex.Message}";
                OnPropertyChanged(nameof(StudyStatus));
            }

            await Task.CompletedTask;
        }

        private async Task GenerateFlashcardsAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedStudyDeck))
            {
                StudyStatus = "Please select or type a deck name first.";
                OnPropertyChanged(nameof(StudyStatus));
                return;
            }

            if (string.IsNullOrWhiteSpace(StudyNotes))
            {
                StudyStatus = "Paste some notes first.";
                OnPropertyChanged(nameof(StudyStatus));
                return;
            }

            try
            {
                StudyStatus = "Generating cards...";
                OnPropertyChanged(nameof(StudyStatus));

                _studyPlugin.CreateDeck(SelectedStudyDeck);

                var prompt = $@"
                Turn the notes into flashcards.
                Return ONLY valid JSON (no markdown, no extra text).
                    JSON shape:
                    [
                      {{ ""front"": ""question"", ""back"": ""answer"" }},
                      ...
                    ]

                Rules:
                - 8 to 15 cards
                - Keep each front/back short and clear
                - Use the notes only (no extra facts)

                NOTES:
                {StudyNotes}
                ";

                var tempHistory = new ChatHistory();
                tempHistory.AddSystemMessage("You generate study flashcards and output ONLY JSON.");
                tempHistory.AddUserMessage(prompt);

                var settings = new OpenAIPromptExecutionSettings
                {
                    
                    Temperature = 0.2
                    
                };

                var reply = await studyChatService.GetChatMessageContentAsync(
                    tempHistory,
                    executionSettings: settings);

                var json = reply.Content?.Trim();
                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidOperationException("Model returned empty response.");

                var cards = JsonSerializer.Deserialize<List<StudyPlugin.NewCard>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (cards == null || cards.Count == 0)
                    throw new InvalidOperationException("No cards were produced from the notes.");

                _studyPlugin.AddFlashcards(SelectedStudyDeck, cards);

                StudyStatus = $"Added {cards.Count} cards to {SelectedStudyDeck}.";
                StudyNotes = string.Empty;

                OnPropertyChanged(nameof(StudyNotes));
                OnPropertyChanged(nameof(StudyStatus));

                await RefreshDecksAsync();
            }
            catch (JsonException)
            {
                StudyStatus = "Couldn’t parse the JSON from the model. Try again (or shorten notes).";
                OnPropertyChanged(nameof(StudyStatus));
            }
            catch (Exception ex)
            {
                StudyStatus = $"Generate failed: {ex.Message}";
                OnPropertyChanged(nameof(StudyStatus));
            }
        }

        private async Task NextCardAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedStudyDeck))
            {
                StudyStatus = "Select a deck first.";
                OnPropertyChanged(nameof(StudyStatus));
                return;
            }

            try
            {
                _currentCard = _studyPlugin.GetNextDueCard(SelectedStudyDeck);

                if (_currentCard == null)
                {
                    CurrentCardFront = "No cards due right now.";
                    CurrentCardBack = "";
                    StudyStatus = "";
                }
                else
                {
                    CurrentCardFront = _currentCard.Front;
                    CurrentCardBack = "";
                    StudyStatus = "Answer hidden. Click 'Show Answer'.";
                }

                OnPropertyChanged(nameof(CurrentCardFront));
                OnPropertyChanged(nameof(CurrentCardBack));
                OnPropertyChanged(nameof(StudyStatus));
            }
            catch (Exception ex)
            {
                StudyStatus = $"Next card failed: {ex.Message}";
                OnPropertyChanged(nameof(StudyStatus));
            }

            await Task.CompletedTask;
        }

        private void CreateDeck()
        {
            if (string.IsNullOrWhiteSpace(SelectedStudyDeck))
            {
                StudyStatus = "Type a deck name first.";
                OnPropertyChanged(nameof(StudyStatus));
                return;
            }

            try
            {
                _studyPlugin.CreateDeck(SelectedStudyDeck);
                StudyStatus = $"Deck ready: {SelectedStudyDeck}";
                OnPropertyChanged(nameof(StudyStatus));

                _ = RefreshDecksAsync();
            }
            catch (Exception ex)
            {
                StudyStatus = $"Create deck failed: {ex.Message}";
                OnPropertyChanged(nameof(StudyStatus));
            }
        }

        private void ShowAnswer()
        {
            if (_currentCard == null)
            {
                StudyStatus = "No active card. Click 'Next Card' first.";
                OnPropertyChanged(nameof(StudyStatus));
                return;
            }

            CurrentCardBack = _currentCard.Back;
            StudyStatus = "Rate it: Wrong / Hard / Good / Easy.";

            OnPropertyChanged(nameof(CurrentCardBack));
            OnPropertyChanged(nameof(StudyStatus));
        }

        private void UpdateStudyCommandStates()
        {
            if (CreateDeckCommand != null)
                CreateDeckCommand.IsEnabled = !string.IsNullOrWhiteSpace(SelectedStudyDeck);

            bool hasCard = _currentCard != null;

            if (ShowAnswerCommand != null) ShowAnswerCommand.IsEnabled = hasCard;
            if (RateWrongCommand != null) RateWrongCommand.IsEnabled = hasCard;
            if (RateHardCommand != null) RateHardCommand.IsEnabled = hasCard;
            if (RateGoodCommand != null) RateGoodCommand.IsEnabled = hasCard;
            if (RateEasyCommand != null) RateEasyCommand.IsEnabled = hasCard;

            CommandManager.InvalidateRequerySuggested();
        }

        private void RateCard(int rating)
        {
            if (_currentCard == null)
            {
                StudyStatus = "No card to rate. Click 'Next Card' first.";
                OnPropertyChanged(nameof(StudyStatus));
                UpdateStudyCommandStates();
                return;
            }

            try
            {
                //rating: 1=wrong, 2=hard, 3=good, 4=easy
                StudyStatus = _studyPlugin.ReviewCard(SelectedStudyDeck, _currentCard.Id, rating);
                OnPropertyChanged(nameof(StudyStatus));

                // Clear current card (optional) and hide answer again
                _currentCard = null;
                CurrentCardFront = "";
                CurrentCardBack = "";
                OnPropertyChanged(nameof(CurrentCardFront));
                OnPropertyChanged(nameof(CurrentCardBack));
            }
            catch (Exception ex)
            {
                StudyStatus = $"Rating failed: {ex.Message}";
                OnPropertyChanged(nameof(StudyStatus));
            }

            UpdateStudyCommandStates();
        }



        #endregion
    }
}
