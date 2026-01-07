using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;

namespace SematicKernelWpf.Model
{
    public class StudyStorage
    {
        private readonly string _root;
        public StudyStorage(string appName = "SematicKernelWPFStudy")
        {
            _root = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            if (!System.IO.Directory.Exists(_root))
            {
                System.IO.Directory.CreateDirectory(_root);
            }
        }

        private string DeckPath(string deckName) => Path.Combine(_root, $"{deckName}.json");

        private static string Safe(string name)
        {
            foreach(char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            
            return name;
        }

        public List<string> ListDecks()
        {
            var decks = new List<string>();
            foreach(var file in Directory.GetFiles(_root, "*.json"))
            {
                decks.Add(Path.GetFileNameWithoutExtension(file));
            }
            return decks;
        }

        public Deck LoadDeck(string deckName)
        {
            string path = DeckPath(deckName);
            if (!File.Exists(path)) return new Deck { Name = deckName };

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Deck>(json) ?? new Deck { Name = deckName };
        }

        public void SaveDeck(Deck deck)
        {
            string json = JsonSerializer.Serialize(deck, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DeckPath(deck.Name), json);
        }

    }
}
