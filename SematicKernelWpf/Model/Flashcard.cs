using System;
using System.Collections.Generic;
using System.Text;

namespace SematicKernelWpf.Model
{
    public sealed class Flashcard
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Front { get; set; } = "";
        public string Back { get; set; } = "";

        
        public DateTime DueUtc { get; set; } = DateTime.UtcNow;
        public int IntervalDays { get; set; } = 1;
    }
}
