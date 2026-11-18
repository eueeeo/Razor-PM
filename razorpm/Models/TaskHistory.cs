using System;

namespace razorpm.Models
{
    public class TaskHistory
    {
        public int Id { get; set; }

        public int TaskItemId { get; set; }
        public string TaskTitle { get; set; } = "";
        public string Action { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
    }
}