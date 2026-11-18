using System;

namespace razorpm.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Priority { get; set; } = "low";
        public string Status { get; set; } = "active";

        public int? AssigneeUserId { get; set; }
        public User? AssigneeUser { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int SpentHours { get; set; } = 0;

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        public string SpentHoursText =>
SpentHours > 0 ? $"{SpentHours} ч" : "0 ч";

        public string AssigneeName =>
            AssigneeUser != null ? AssigneeUser.Name : "—";

        public string PriorityRu =>
            Priority == "high" ? "Высокий" :
            Priority == "medium" ? "Средний" :
            "Низкий";

        public string StatusRu =>
            Status == "done" ? "Завершена" : "Активна";

        public string PriorityClass =>
            Priority == "high" ? "badge-high" :
            Priority == "medium" ? "badge-medium" :
            "badge-low";

        public string DueDateText =>
            DueDate.HasValue ? DueDate.Value.ToString("dd.MM.yyyy") : "—";

        public string StartDateText =>
            StartDate.HasValue ? StartDate.Value.ToString("dd.MM.yyyy") : "—";

        public string ProjectTitle =>
            Project != null ? Project.Name : "—";

        public bool IsOverdue =>
            DueDate.HasValue &&
            DueDate.Value.Date < DateTime.Today &&
            Status != "done";
    }
}