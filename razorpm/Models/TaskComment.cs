using System;

namespace razorpm.Models
{
    public class TaskComment
    {
        public int Id { get; set; }

        public int TaskItemId { get; set; }
        public string AuthorName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
    }
}