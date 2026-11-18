using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class ProjectDetailsModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ProjectDetailsModel(AppDbContext db)
        {
            _db = db;
        }

        public Project? Project { get; private set; }
        public List<TaskItem> Tasks { get; private set; } = new();
        public List<User> Members { get; private set; } = new();
        public List<User> AvailableUsers { get; private set; } = new();
        public List<SelectListItem> TaskAssigneeOptions { get; private set; } = new();
        public List<SelectListItem> OwnerOptions { get; private set; } = new();

        public int TasksTotal { get; private set; }
        public int TasksActive { get; private set; }
        public int TasksDone { get; private set; }
        public int TasksOverdue { get; private set; }
        public int ProgressPercent { get; private set; }

        public bool CanManageCurrentProject { get; private set; }
        public bool CanChangeProjectOwner => IsDirector && Project != null && !Project.IsArchived;

        public List<TaskHistory> RecentHistory { get; private set; } = new();

        [TempData]
        public string? ProjectError { get; set; }

        [TempData]
        public string? ProjectMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? taskQ { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? taskStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? taskAssigneeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? taskSort { get; set; }

        public IActionResult OnGet(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadProject(id))
            {
                return RedirectToPage("/Projects");
            }

            return Page();
        }

        public IActionResult OnPostChangeOwner(int id, int newOwnerId)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!IsDirector)
            {
                return RedirectToPage("/Projects");
            }

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == id);

            if (project == null)
            {
                return RedirectToPage("/Projects");
            }

            if (!CanViewProject(project))
            {
                return RedirectToPage("/Projects");
            }

            if (project.IsArchived)
            {
                ProjectError = "Нельзя менять ответственного у архивного проекта";
                return RedirectToPage(new { id });
            }

            var newOwner = _db.Users.FirstOrDefault(u =>
                u.Id == newOwnerId &&
                u.IsActive &&
                u.Role == "manager");

            if (newOwner == null)
            {
                ProjectError = "Можно выбрать только активного ответственного";
                return RedirectToPage(new { id });
            }

            if (project.OwnerId == newOwner.Id)
            {
                ProjectMessage = "Этот пользователь уже является ответственным за проект";
                return RedirectToPage(new { id });
            }

            var oldOwnerId = project.OwnerId;
            var oldOwnerName = string.IsNullOrWhiteSpace(project.OwnerName)
                ? "—"
                : project.OwnerName;

            var oldOwner = _db.Users.FirstOrDefault(u => u.Id == oldOwnerId);

            project.OwnerId = newOwner.Id;
            project.OwnerName = newOwner.Name;

            var newOwnerMember = _db.ProjectMembers.FirstOrDefault(pm =>
                pm.ProjectId == project.Id &&
                pm.UserId == newOwner.Id);

            if (newOwnerMember != null)
            {
                _db.ProjectMembers.Remove(newOwnerMember);
            }

            if (oldOwner != null &&
                oldOwner.IsActive &&
                oldOwner.Id != newOwner.Id &&
                oldOwner.Role != "admin" &&
                oldOwner.Role != "director")
            {
                var oldOwnerAlreadyMember = _db.ProjectMembers.Any(pm =>
                    pm.ProjectId == project.Id &&
                    pm.UserId == oldOwner.Id);

                if (!oldOwnerAlreadyMember)
                {
                    _db.ProjectMembers.Add(new ProjectMember
                    {
                        ProjectId = project.Id,
                        UserId = oldOwner.Id
                    });
                }
            }

            _db.SaveChanges();

            ProjectMessage = $"Ответственный изменён: {oldOwnerName} ? {newOwner.Name}";

            return RedirectToPage(new { id });
        }

        public IActionResult OnPostAddMember(int id, int userId)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (id <= 0)
            {
                return RedirectToPage("/Projects");
            }

            if (userId <= 0)
            {
                ProjectError = "Выберите пользователя";
                return RedirectToPage(new { id });
            }

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == id);

            if (project == null)
            {
                return RedirectToPage("/Projects");
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage("/Projects");
            }

            if (project.IsArchived)
            {
                ProjectError = "Нельзя менять участников архивного проекта";
                return RedirectToPage(new { id });
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                ProjectError = "Пользователь не найден";
                return RedirectToPage(new { id });
            }

            if (!user.IsActive)
            {
                ProjectError = "Нельзя добавить отключённого пользователя";
                return RedirectToPage(new { id });
            }

            if (user.Role == "admin" || user.Role == "director")
            {
                ProjectError = "В проект можно добавлять только ответственных и исполнителей";
                return RedirectToPage(new { id });
            }

            if (project.OwnerId == userId)
            {
                ProjectError = "Ответственный уже имеет доступ к проекту";
                return RedirectToPage(new { id });
            }

            var alreadyExists = _db.ProjectMembers.Any(x =>
                x.ProjectId == id &&
                x.UserId == userId);

            if (alreadyExists)
            {
                ProjectError = "Этот пользователь уже добавлен в проект";
                return RedirectToPage(new { id });
            }

            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = id,
                UserId = userId
            });

            _db.SaveChanges();

            ProjectMessage = "Участник добавлен";

            return RedirectToPage(new { id });
        }

        public IActionResult OnPostRemoveMember(int id, int userId)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (id <= 0)
            {
                return RedirectToPage("/Projects");
            }

            if (userId <= 0)
            {
                ProjectError = "Пользователь не выбран";
                return RedirectToPage(new { id });
            }

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == id);

            if (project == null)
            {
                return RedirectToPage("/Projects");
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage("/Projects");
            }

            if (project.IsArchived)
            {
                ProjectError = "Нельзя менять участников архивного проекта";
                return RedirectToPage(new { id });
            }

            if (project.OwnerId == userId)
            {
                ProjectError = "Нельзя удалить ответственного за проект";
                return RedirectToPage(new { id });
            }

            var member = _db.ProjectMembers.FirstOrDefault(x =>
                x.ProjectId == id &&
                x.UserId == userId);

            if (member == null)
            {
                ProjectError = "Участник не найден в проекте";
                return RedirectToPage(new { id });
            }

            var hasActiveTasks = _db.Tasks.Any(t =>
                t.ProjectId == id &&
                t.AssigneeUserId == userId &&
                t.Status != "done");

            if (hasActiveTasks)
            {
                ProjectError = "Нельзя удалить участника, пока у него есть активные задачи в проекте";
                return RedirectToPage(new { id });
            }

            _db.ProjectMembers.Remove(member);
            _db.SaveChanges();

            ProjectMessage = "Участник удалён из проекта";

            return RedirectToPage(new { id });
        }

        public IActionResult OnPostChangeTaskStatus(int id, int taskId)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadProject(id))
            {
                return RedirectToPage("/Projects");
            }

            var task = _db.Tasks
                .FirstOrDefault(t => t.Id == taskId && t.ProjectId == id);

            if (task == null)
            {
                return RedirectToPage(new
                {
                    id,
                    taskQ,
                    taskStatus,
                    taskAssigneeId,
                    taskSort
                });
            }

            if (!CanChangeTaskStatus(task))
            {
                return RedirectToPage(new
                {
                    id,
                    taskQ,
                    taskStatus,
                    taskAssigneeId,
                    taskSort
                });
            }

            task.Status = task.Status == "done" ? "active" : "done";

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = task.Id,
                TaskTitle = task.Title,
                Action = task.Status == "done" ? "Задача завершена" : "Задача возвращена в работу",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            SyncProjectStatus(_db, id);
            _db.SaveChanges();

            return RedirectToPage(new
            {
                id,
                taskQ,
                taskStatus,
                taskAssigneeId,
                taskSort
            });
        }

        private bool LoadProject(int id)
        {
            Project = _db.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefault(p => p.Id == id);

            if (Project == null)
            {
                return false;
            }

            if (!CanViewProject(Project))
            {
                return false;
            }

            CanManageCurrentProject = CanManageProject(Project);

            OwnerOptions = _db.Users
                .Where(u =>
                    u.IsActive &&
                    u.Role == "manager" &&
                    u.Id != Project.OwnerId)
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Login)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Name} ({u.Login})"
                })
                .ToList();

            var allTasks = _db.Tasks
                .Include(t => t.AssigneeUser)
                .Where(t => t.ProjectId == id)
                .ToList();

            var taskIds = allTasks.Select(t => t.Id).ToList();

            RecentHistory = _db.TaskHistories
                .Where(h => taskIds.Contains(h.TaskItemId))
                .OrderByDescending(h => h.CreatedAt)
                .Take(5)
                .ToList();

            Members = Project.Members
                .Where(x =>
                    x.User != null &&
                    x.UserId != Project.OwnerId)
                .Select(x => x.User!)
                .OrderBy(x => x.Name)
                .ToList();

            var excludedIds = Members.Select(x => x.Id).ToList();
            excludedIds.Add(Project.OwnerId);

            AvailableUsers = _db.Users
                .Where(u =>
                    u.IsActive &&
                    u.Role != "admin" &&
                    u.Role != "director" &&
                    !excludedIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .ToList();

            var projectUserIds = Project.Members
                .Select(m => m.UserId)
                .Append(Project.OwnerId)
                .Distinct()
                .ToList();

            var assignedUserIds = allTasks
                .Where(t => t.AssigneeUserId.HasValue)
                .Select(t => t.AssigneeUserId!.Value)
                .Distinct()
                .ToList();

            var taskAssigneeIds = projectUserIds
                .Union(assignedUserIds)
                .Distinct()
                .ToList();

            TaskAssigneeOptions = _db.Users
                .Where(u => u.IsActive && taskAssigneeIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Name} ({u.Login})"
                })
                .ToList();

            TasksTotal = allTasks.Count;
            TasksActive = allTasks.Count(t => t.Status != "done");
            TasksDone = allTasks.Count(t => t.Status == "done");
            TasksOverdue = allTasks.Count(t => t.IsOverdue);
            ProgressPercent = TasksTotal == 0 ? 0 : (int)((double)TasksDone / TasksTotal * 100);

            var filteredTasks = allTasks.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(taskQ))
            {
                var search = taskQ.Trim();

                filteredTasks = filteredTasks.Where(t =>
                    (t.Title ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (t.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (t.AssigneeName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (taskStatus == "active" || taskStatus == "done")
            {
                filteredTasks = filteredTasks.Where(t => t.Status == taskStatus);
            }

            if (taskAssigneeId.HasValue)
            {
                filteredTasks = filteredTasks.Where(t => t.AssigneeUserId == taskAssigneeId.Value);
            }

            Tasks = taskSort switch
            {
                "priority" => filteredTasks
                    .OrderBy(t => GetPriorityOrder(t.Priority))
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

                "start" => filteredTasks
                    .OrderBy(t => t.StartDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList(),

                _ => filteredTasks
                    .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.Title)
                    .ToList()
            };

            return true;
        }

        public new bool CanManageTask(TaskItem task)
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

            if (Project == null)
            {
                return false;
            }

            var userId = CurrentUserId.Value;

            return Project.OwnerId == userId ||
                   Project.Members.Any(m => m.UserId == userId);
        }

        public bool CanChangeTaskStatus(TaskItem task)
        {
            if (CanManageTask(task))
            {
                return true;
            }

            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsAdmin)
            {
                return false;
            }

            return task.AssigneeUserId == CurrentUserId.Value;
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