using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class ProfileModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ProfileModel(AppDbContext db)
        {
            _db = db;
        }

        public User? CurrentUser { get; private set; }

        public string RoleTitle { get; private set; } = "";
        public string AccountStatusText { get; private set; } = "";

        public int OwnerProjectsCount { get; private set; }
        public int MemberProjectsCount { get; private set; }

        public int AssignedTasksCount { get; private set; }
        public int ActiveTasksCount { get; private set; }
        public int DoneTasksCount { get; private set; }
        public int OverdueTasksCount { get; private set; }

        public int SpentHoursTotal { get; private set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            var userId = CurrentUserId.Value;

            CurrentUser = _db.Users.FirstOrDefault(u => u.Id == userId);

            if (CurrentUser == null || !CurrentUser.IsActive)
            {
                return RedirectToPage("/Login");
            }

            RoleTitle = GetRoleTitle(CurrentUser.Role);
            AccountStatusText = CurrentUser.IsActive ? "Активен" : "Отключён";

            OwnerProjectsCount = _db.Projects.Count(p => p.OwnerId == userId);
            MemberProjectsCount = _db.ProjectMembers.Count(pm => pm.UserId == userId);

            var myTasks = _db.Tasks
                .Where(t => t.AssigneeUserId == userId)
                .ToList();

            AssignedTasksCount = myTasks.Count;
            ActiveTasksCount = myTasks.Count(t => t.Status != "done");
            DoneTasksCount = myTasks.Count(t => t.Status == "done");
            OverdueTasksCount = myTasks.Count(t => t.IsOverdue);
            SpentHoursTotal = myTasks.Sum(t => t.SpentHours);

            return Page();
        }

        private static string GetRoleTitle(string? role)
        {
            return role switch
            {
                "admin" => "Администратор",
                "director" => "Руководитель",
                "manager" => "Ответственный",
                _ => "Исполнитель"
            };
        }
    }
}