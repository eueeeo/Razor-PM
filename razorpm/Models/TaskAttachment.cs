using System;

namespace razorpm.Models
{
    public class TaskAttachment
    {
        public int Id { get; set; }

        public int TaskItemId { get; set; }
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
    }
}