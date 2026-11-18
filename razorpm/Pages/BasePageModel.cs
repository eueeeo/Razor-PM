using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using razorpm.Data;
using razorpm.Helpers;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class BasePageModel : PageModel
    {
        public string CurrentName =>
            HttpContext.Session.GetString(SessionKeys.Name) ?? "";

        public string CurrentRole =>
            HttpContext.Session.GetString(SessionKeys.Role) ?? "user";

        public int? CurrentUserId
        {
            get
            {
                var value = HttpContext.Session.GetString(SessionKeys.UserId);

                if (int.TryParse(value, out var id))
                {
                    return id;
                }

                return null;
            }
        }

        public bool IsAdmin => CurrentRole == "admin";
        public bool IsDirector => CurrentRole == "director";
        public bool IsManager => CurrentRole == "manager";
        public bool IsExecutor => CurrentRole == "user";

        public string CurrentRoleTitle =>
            CurrentRole switch
            {
                "admin" => "Администратор",
                "director" => "Руководитель",
                "manager" => "Ответственный",
                _ => "Исполнитель"
            };

        protected IActionResult? RequireLogin()
        {
            var userId = HttpContext.Session.GetString(SessionKeys.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Login");
            }

            return null;
        }

        protected IActionResult? RequireAdmin()
        {
            var loginCheck = RequireLogin();
            if (loginCheck != null) return loginCheck;

            if (!IsAdmin)
            {
                return RedirectToPage("/Index");
            }

            return null;
        }

        protected IActionResult? RequireDirectorOrManager()
        {
            var loginCheck = RequireLogin();
            if (loginCheck != null) return loginCheck;

            if (!IsDirector && !IsManager)
            {
                return RedirectToPage("/Index");
            }

            return null;
        }

        public bool CanCreateProject()
        {
            return IsDirector || IsManager;
        }

        public bool CanCreateTask()
        {
            return IsDirector || IsManager;
        }

        public bool CanViewProject(Project project)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            var userId = CurrentUserId.Value;

            return project.OwnerId == userId ||
                   project.Members.Any(m => m.UserId == userId);
        }

        public bool CanViewProject(Project project, bool isMember)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            return project.OwnerId == CurrentUserId.Value || isMember;
        }

        public bool CanManageProject(Project project)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            if (!IsManager)
            {
                return false;
            }

            var userId = CurrentUserId.Value;

            return project.OwnerId == userId ||
                   project.Members.Any(m => m.UserId == userId);
        }

        public bool CanViewTask(TaskItem task)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            var userId = CurrentUserId.Value;

            if (task.AssigneeUserId == userId)
            {
                return true;
            }

            if (task.Project == null)
            {
                return false;
            }

            return task.Project.OwnerId == userId ||
                   task.Project.Members.Any(m => m.UserId == userId);
        }

        public bool CanManageTask(TaskItem task)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            if (!IsManager)
            {
                return false;
            }

            var userId = CurrentUserId.Value;

            if (task.Project != null)
            {
                return task.Project.OwnerId == userId ||
                       task.Project.Members.Any(m => m.UserId == userId);
            }

            return false;
        }

        public bool CanChangeOwnTaskStatus(TaskItem task)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            return task.AssigneeUserId == CurrentUserId.Value ||
                   CanManageTask(task);
        }

        protected void SyncProjectStatus(AppDbContext db, int? projectId)
        {
            if (!projectId.HasValue)
            {
                return;
            }

            var project = db.Projects.FirstOrDefault(p => p.Id == projectId.Value);
            if (project == null)
            {
                return;
            }

            var tasks = db.Tasks
                .Where(t => t.ProjectId == projectId.Value)
                .ToList();

            if (tasks.Count == 0)
            {
                return;
            }

            var hasActive = tasks.Any(t => t.Status != "done");
            project.Status = hasActive ? "active" : "done";
        }
    }
}