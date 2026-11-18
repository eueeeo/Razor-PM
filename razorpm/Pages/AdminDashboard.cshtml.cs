using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Models;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class AdminDashboardModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public AdminDashboardModel(AppDbContext db)
        {
            _db = db;
        }

        public int TotalUsers { get; private set; }
        public int ActiveUsers { get; private set; }
        public int DisabledUsers { get; private set; }

        public int Admins { get; private set; }
        public int Directors { get; private set; }
        public int Managers { get; private set; }
        public int Executors { get; private set; }

        public int TotalProjects { get; private set; }
        public int ActiveProjects { get; private set; }
        public int DoneProjects { get; private set; }
        public int ArchivedProjects { get; private set; }

        public int TotalTasks { get; private set; }
        public int ActiveTasks { get; private set; }
        public int DoneTasks { get; private set; }
        public int OverdueTasks { get; private set; }
        public int TotalHours { get; private set; }

        public List<TaskHistory> RecentHistory { get; private set; } = new();

        public IActionResult OnGet()
        {
            var gate = RequireAdmin();
            if (gate != null) return gate;

            TotalUsers = _db.Users.Count();
            ActiveUsers = _db.Users.Count(u => u.IsActive);
            DisabledUsers = _db.Users.Count(u => !u.IsActive);

            Admins = _db.Users.Count(u => u.Role == "admin");
            Directors = _db.Users.Count(u => u.Role == "director");
            Managers = _db.Users.Count(u => u.Role == "manager");
            Executors = _db.Users.Count(u => u.Role == "user");

            TotalProjects = _db.Projects.Count();
            ActiveProjects = _db.Projects.Count(p => p.Status != "done" && !p.IsArchived);
            DoneProjects = _db.Projects.Count(p => p.Status == "done" && !p.IsArchived);
            ArchivedProjects = _db.Projects.Count(p => p.IsArchived);

            TotalTasks = _db.Tasks.Count();
            ActiveTasks = _db.Tasks.Count(t => t.Status != "done");
            DoneTasks = _db.Tasks.Count(t => t.Status == "done");
            OverdueTasks = _db.Tasks.ToList().Count(t => t.IsOverdue);
            TotalHours = _db.Tasks.Sum(t => t.SpentHours);

            RecentHistory = _db.TaskHistories
                .OrderByDescending(h => h.CreatedAt)
                .Take(5)
                .ToList();

            return Page();
        }
    }
}