using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace razorpm.Pages
{
    public class TasksModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public TasksModel(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<TaskItem> Tasks { get; private set; } = new();
        public List<Project> Projects { get; private set; } = new();
        public List<SelectListItem> AssigneeOptions { get; private set; } = new();

        public bool CanCreateNewTask => IsDirector || IsManager;
        public bool CanUseAssigneeFilter => IsDirector || IsManager;

        [BindProperty(SupportsGet = true)] public string? q { get; set; }
        [BindProperty(SupportsGet = true)] public string? status { get; set; }
        [BindProperty(SupportsGet = true)] public int? projectId { get; set; }
        [BindProperty(SupportsGet = true)] public int? assigneeId { get; set; }
        [BindProperty(SupportsGet = true)] public string? archive { get; set; }
        [BindProperty(SupportsGet = true)] public string? sort { get; set; }
        [BindProperty(SupportsGet = true)] public string? scope { get; set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (IsAdmin)
            {
                return RedirectToPage("/AdminDashboard");
            }

            var projectsQuery = _db.Projects
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .AsQueryable();

            if (archive == "archived")
            {
                projectsQuery = projectsQuery.Where(p => p.IsArchived);
            }
            else
            {
                projectsQuery = projectsQuery.Where(p => !p.IsArchived);
            }

            if (IsDirector)
            {
            }
            else if (IsManager && CurrentUserId.HasValue)
            {
                var userId = CurrentUserId.Value;

                projectsQuery = projectsQuery.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m => m.UserId == userId));
            }
            else if (CurrentUserId.HasValue)
            {
                var userId = CurrentUserId.Value;

                projectsQuery = projectsQuery.Where(p =>
                    p.Tasks.Any(t => t.AssigneeUserId == userId));
            }
            else
            {
                projectsQuery = projectsQuery.Where(p => false);
            }

            Projects = projectsQuery
                .OrderBy(p => p.Name)
                .ToList();

            var projectIds = Projects
                .Select(p => p.Id)
                .ToList();

            LoadAssigneeOptions(projectIds);

            var query = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .Include(t => t.AssigneeUser)
                .AsQueryable();

            if (IsDirector)
            {
                query = query.Where(t =>
                    t.ProjectId.HasValue &&
                    projectIds.Contains(t.ProjectId.Value));
            }
            else if (IsManager)
            {
                query = query.Where(t =>
                    t.ProjectId.HasValue &&
                    projectIds.Contains(t.ProjectId.Value));
            }
            else if (CurrentUserId.HasValue)
            {
                var userId = CurrentUserId.Value;

                query = query.Where(t =>
                    t.AssigneeUserId == userId &&
                    t.ProjectId.HasValue &&
                    projectIds.Contains(t.ProjectId.Value));
            }
            else
            {
                query = query.Where(t => false);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();

                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.AssigneeUser != null && t.AssigneeUser.Name.Contains(search)) ||
                    (t.Project != null && t.Project.Name.Contains(search)));
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            if (scope == "my" && CurrentUserId.HasValue)
            {
                query = query.Where(t => t.AssigneeUserId == CurrentUserId.Value);
            }

            if (scope == "today")
            {
                query = query.Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate.Value >= today &&
                    t.DueDate.Value < tomorrow);
            }

            if (scope == "overdue")
            {
                query = query.Where(t =>
                    t.Status != "done" &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value < today);
            }

            if (status == "active" || status == "done")
            {
                query = query.Where(t => t.Status == status);
            }

            if (projectId.HasValue)
            {
                query = query.Where(t => t.ProjectId == projectId.Value);
            }

            if (assigneeId.HasValue && CanUseAssigneeFilter)
            {
                query = query.Where(t => t.AssigneeUserId == assigneeId.Value);
            }

            var filteredTasks = query.ToList();

            Tasks = sort switch
            {
                "priority" => filteredTasks
                    .OrderBy(t => GetPriorityOrder(t.Priority))
                    .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList(),

                "project" => filteredTasks
                    .OrderBy(t => t.ProjectTitle)
                    .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList(),

                "assignee" => filteredTasks
                    .OrderBy(t => t.AssigneeName)
                    .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList(),

                "status" => filteredTasks
                    .OrderBy(t => t.Status)
                    .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList(),

                _ => filteredTasks
                    .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList()
            };

            return Page();
        }

        public string GetMyRole(TaskItem task)
        {
            if (IsDirector)
            {
                return "Руководитель";
            }

            if (CanManageTask(task))
            {
                return "Ответственный";
            }

            if (CurrentUserId.HasValue && task.AssigneeUserId == CurrentUserId.Value)
            {
                return "Исполнитель";
            }

            return "Участник";
        }

        public IActionResult OnPostChangeStatus(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (IsAdmin)
            {
                return RedirectToPage("/AdminDashboard");
            }

            var task = _db.Tasks
                .Include(t => t.Project)
                .FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
            }

            if (!CanChangeOwnTaskStatus(task) && !CanManageTask(task))
            {
                return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
            }

            var oldStatus = task.Status;
            task.Status = task.Status == "done" ? "active" : "done";

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = task.Id,
                TaskTitle = task.Title,
                Action = oldStatus == "done"
                    ? "Статус изменен: завершена → активна"
                    : "Статус изменен: активна → завершена",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            SyncProjectStatus(_db, task.ProjectId);
            _db.SaveChanges();

            return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
        }

        public IActionResult OnPostDelete(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (IsAdmin)
            {
                return RedirectToPage("/AdminDashboard");
            }

            var task = _db.Tasks
                .Include(t => t.Project)
                .FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
            }

            if (!CanManageTask(task))
            {
                return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
            }

            var projectIdToSync = task.ProjectId;

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = task.Id,
                TaskTitle = task.Title,
                Action = "Удаление задачи",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            DeleteTaskWithRelatedData(task);
            _db.SaveChanges();

            SyncProjectStatus(_db, projectIdToSync);
            _db.SaveChanges();

            return RedirectToPage("/Tasks", new { q, status, projectId, assigneeId, archive, sort, scope });
        }

        private void DeleteTaskWithRelatedData(TaskItem task)
        {
            var comments = _db.TaskComments
                .Where(c => c.TaskItemId == task.Id)
                .ToList();

            var attachments = _db.TaskAttachments
                .Where(a => a.TaskItemId == task.Id)
                .ToList();

            foreach (var attachment in attachments)
            {
                DeletePhysicalFile(attachment.FilePath);
            }

            _db.TaskComments.RemoveRange(comments);
            _db.TaskAttachments.RemoveRange(attachments);
            _db.Tasks.Remove(task);
        }

        private void DeletePhysicalFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var relativePath = filePath
                .TrimStart('/')
                .Replace("/", Path.DirectorySeparatorChar.ToString());

            var fullPath = Path.Combine(_env.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private void LoadAssigneeOptions(List<int> projectIds)
        {
            if (!CanUseAssigneeFilter || projectIds.Count == 0)
            {
                AssigneeOptions = new List<SelectListItem>();
                return;
            }

            AssigneeOptions = _db.Users
                .Where(u =>
                    u.IsActive &&
                    u.Role != "admin" &&
                    _db.Tasks.Any(t =>
                        t.AssigneeUserId == u.Id &&
                        t.ProjectId.HasValue &&
                        projectIds.Contains(t.ProjectId.Value)))
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Login)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Name} ({u.Login})"
                })
                .ToList();
        }

        private static int GetPriorityOrder(string? priority)
        {
            return priority switch
            {
                "high" => 0,
                "medium" => 1,
                _ => 2
            };
        }
    }
}
