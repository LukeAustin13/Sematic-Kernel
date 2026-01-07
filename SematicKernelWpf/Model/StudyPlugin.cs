using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SematicKernelWpf.Model
{
    public sealed class StudyPlugin
    {

        private readonly StudyStorage _storage;

        public StudyPlugin(StudyStorage storage) => _storage = storage;

        public sealed class NewCard
        {
            public string Front { get; set; } = "";
            public string Back { get; set; } = "";
        }

        [KernelFunction, Description("List available study decks.")]
        public List<string> ListDecks() => _storage.ListDecks();

        [KernelFunction, Description("Create an empty deck if it doesn't exist.")]
        public string CreateDeck(string deckName)
        {
            var deck = _storage.LoadDeck(deckName);
            deck.Name = deckName;
            _storage.SaveDeck(deck);
            return $"Deck ready: {deckName}";
        }

        [KernelFunction, Description("Add flashcards to a deck.")]
        public string AddFlashcards(string deckName, List<NewCard> cards)
        {
            var deck = _storage.LoadDeck(deckName);
            deck.Name = deckName;

            foreach (var c in cards)
            {
                deck.Cards.Add(new Flashcard
                {
                    Front = c.Front,
                    Back = c.Back,
                    DueUtc = DateTime.UtcNow
                });
            }

            _storage.SaveDeck(deck);
            return $"Added {cards.Count} cards to {deckName}.";
        }

        [KernelFunction, Description("Get the next due flashcard to study.")]
        public Flashcard? GetNextDueCard(string deckName)
        {
            var deck = _storage.LoadDeck(deckName);
            return deck.Cards
                .Where(c => c.DueUtc <= DateTime.UtcNow)
                .OrderBy(c => c.DueUtc)
                .FirstOrDefault();
        }

        [KernelFunction, Description("Record a review result: 1=wrong, 2=hard, 3=good, 4=easy.")]
        public string ReviewCard(string deckName, string cardId, int rating)
        {
            var deck = _storage.LoadDeck(deckName);
            var card = deck.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return "Card not found.";

            
            if (rating <= 1)
                card.IntervalDays = 1;
            else if (rating == 2)
                card.IntervalDays = Math.Max(1, card.IntervalDays);
            else if (rating == 3)
                card.IntervalDays = Math.Min(30, card.IntervalDays * 2);
            else
                card.IntervalDays = Math.Min(60, card.IntervalDays * 3);

            card.DueUtc = DateTime.UtcNow.AddDays(card.IntervalDays);
            _storage.SaveDeck(deck);

            return $"Saved. Next due: {card.DueUtc:u} (in {card.IntervalDays} days)";
        }

    }
}
