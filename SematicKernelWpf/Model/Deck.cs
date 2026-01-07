using System;
using System.Collections.Generic;
using System.Text;

namespace SematicKernelWpf.Model
{
    public sealed class Deck
    {
        public string Name { get; set; } = "";
        public List<Flashcard> Cards { get; set; } = new();

    }
}
