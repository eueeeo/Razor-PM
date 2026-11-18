using System.Collections.Generic;
using System.Linq;

namespace razorpm.Models
{
    public class Project
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string Status { get; set; } = "active";
        public bool IsArchived { get; set; } = false;

        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "";

        public List<TaskItem> Tasks { get; set; } = new();
        public List<ProjectMember> Members { get; set; } = new();

        public int TasksCount => Tasks.Count;
        public int ActiveTasks => Tasks.Count(t => t.Status != "done");
        public int DoneTasks => Tasks.Count(t => t.Status == "done");

        public int MembersCount => Members.Count;

        public int ProgressPercent
        {
            get
            {
                if (TasksCount == 0) return 0;
                return (int)((double)DoneTasks / TasksCount * 100);
            }
        }
    }
}