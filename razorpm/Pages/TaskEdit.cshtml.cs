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
    public class TaskEditModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public TaskEditModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty] public int Id { get; set; }
        [BindProperty] public string Title { get; set; } = "";
        [BindProperty] public string Description { get; set; } = "";
        [BindProperty] public string Priority { get; set; } = "low";
        [BindProperty] public string Status { get; set; } = "active";
        [BindProperty] public DateTime? StartDate { get; set; }
        [BindProperty] public DateTime? DueDate { get; set; }
        [BindProperty] public int? SpentHours { get; set; }
        [BindProperty] public int? ProjectId { get; set; }
        [BindProperty] public int? AssigneeUserId { get; set; }

        public string Error { get; set; } = "";
        public List<SelectListItem> ProjectOptions { get; private set; } = new();
        public List<SelectListItem> AssigneeOptions { get; private set; } = new();

        public IActionResult OnGet(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            var task = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                return RedirectToPage("/Tasks");
            }

            if (!CanEditTask(task))
            {
                return RedirectToPage("/Tasks");
            }

            Id = task.Id;
            Title = task.Title ?? "";
            Description = task.Description ?? "";
            Priority = task.Priority ?? "low";
            Status = task.Status ?? "active";
            AssigneeUserId = task.AssigneeUserId;
            StartDate = task.StartDate;
            DueDate = task.DueDate;
            SpentHours = task.SpentHours;
            ProjectId = task.ProjectId;

            LoadProjects();
            LoadAssignees(ProjectId);

            return Page();
        }

        public JsonResult OnGetAssignees(int? projectId)
        {
            if (RequireLogin() != null)
            {
                Response.StatusCode = 401;
                return new JsonResult(Array.Empty<object>());
            }

            if (!CanCreateTask())
            {
                Response.StatusCode = 403;
                return new JsonResult(Array.Empty<object>());
            }

            var users = GetProjectUsers(projectId)
                .Select(u => new
                {
                    value = u.Id.ToString(),
                    text = $"{u.Name} ({u.Login})"
                })
                .ToList();

            return new JsonResult(users);
        }

        public IActionResult OnPost()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanCreateTask())
            {
                return RedirectToPage("/Tasks");
            }

            Title = (Title ?? "").Trim();
            Description = (Description ?? "").Trim();
            Priority = NormalizePriority(Priority);
            Status = NormalizeStatus(Status);
            SpentHours ??= 0;

            LoadProjects();
            LoadAssignees(ProjectId);

            if (string.IsNullOrWhiteSpace(Title))
            {
                Error = "Введите название задачи";
                return Page();
            }

            if (Title.Length < 3)
            {
                Error = "Название задачи должно быть не короче 3 символов";
                return Page();
            }

            if (Title.Length > 120)
            {
                Error = "Название задачи не должно быть длиннее 120 символов";
                return Page();
            }

            if (Description.Length > 2000)
            {
                Error = "Описание задачи не должно быть длиннее 2000 символов";
                return Page();
            }

            if (!ProjectId.HasValue)
            {
                Error = "Выберите проект";
                return Page();
            }

            if (!AssigneeUserId.HasValue)
            {
                Error = "Выберите исполнителя";
                return Page();
            }

            if (!DueDate.HasValue)
            {
                Error = "Укажите срок выполнения";
                return Page();
            }

            if (!StartDate.HasValue)
            {
                StartDate = DateTime.Today;
            }

            if (StartDate.Value.Date > DueDate.Value.Date)
            {
                Error = "Дата начала не может быть позже срока выполнения";
                return Page();
            }

            if (SpentHours.Value < 0)
            {
                Error = "Количество часов не может быть отрицательным";
                return Page();
            }

            if (SpentHours.Value > 1000)
            {
                Error = "Количество часов слишком большое";
                return Page();
            }

            var task = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .FirstOrDefault(t => t.Id == Id);

            if (task == null)
            {
                return RedirectToPage("/Tasks");
            }

            if (!CanEditTask(task))
            {
                return RedirectToPage("/Tasks");
            }

            var newProject = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == ProjectId.Value && !p.IsArchived);

            if (newProject == null)
            {
                Error = "Проект не найден или находится в архиве";
                return Page();
            }

            if (!CanManageProject(newProject))
            {
                Error = "Нет прав на редактирование задач этого проекта";
                return Page();
            }

            var allowedUserIds = GetProjectUsers(ProjectId)
                .Select(u => u.Id)
                .ToList();

            if (!allowedUserIds.Contains(AssigneeUserId.Value))
            {
                Error = "Исполнитель не относится к этому проекту";
                LoadAssignees(ProjectId);
                return Page();
            }

            var assigneeIsActive = _db.Users.Any(u =>
                u.Id == AssigneeUserId.Value &&
                u.IsActive &&
                u.Role != "admin" &&
                u.Role != "director");

            if (!assigneeIsActive)
            {
                Error = "Выбранный исполнитель не найден или отключён";
                return Page();
            }

            var oldProjectId = task.ProjectId;

            var historyActions = BuildTaskChangeHistory(
                task,
                Title,
                Description,
                Priority,
                Status,
                StartDate,
                DueDate,
                SpentHours.Value,
                ProjectId,
                AssigneeUserId);

            task.Title = Title;
            task.Description = Description;
            task.Priority = Priority;
            task.Status = Status;
            task.AssigneeUserId = AssigneeUserId;
            task.StartDate = StartDate;
            task.DueDate = DueDate;
            task.SpentHours = SpentHours.Value;
            task.ProjectId = ProjectId;

            _db.SaveChanges();

            SyncProjectStatus(_db, oldProjectId);
            SyncProjectStatus(_db, task.ProjectId);
            _db.SaveChanges();

            foreach (var action in historyActions)
            {
                _db.TaskHistories.Add(new TaskHistory
                {
                    TaskItemId = task.Id,
                    TaskTitle = task.Title,
                    Action = action,
                    UserName = CurrentName,
                    CreatedAt = DateTime.Now
                });
            }

            if (historyActions.Count > 0)
            {
                _db.SaveChanges();
            }

            return RedirectToPage("/TaskDetails", new { id = task.Id });
        }

        private bool CanEditTask(TaskItem task)
        {
            return CanManageTask(task);
        }

        private void LoadProjects()
        {
            if (!CurrentUserId.HasValue)
            {
                ProjectOptions = new List<SelectListItem>();
                return;
            }

            var query = _db.Projects
                .Include(p => p.Members)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (IsDirector)
            {
            }
            else if (IsManager)
            {
                var userId = CurrentUserId.Value;

                query = query.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m => m.UserId == userId));
            }
            else
            {
                query = query.Where(p => false);
            }

            ProjectOptions = query
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToList();
        }

        private void LoadAssignees(int? selectedProjectId)
        {
            AssigneeOptions = GetProjectUsers(selectedProjectId)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Name} ({u.Login})"
                })
                .ToList();
        }

        private List<User> GetProjectUsers(int? selectedProjectId)
        {
            if (!selectedProjectId.HasValue)
            {
                return new List<User>();
            }

            var project = _db.Projects
                .Include(p => p.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefault(p => p.Id == selectedProjectId.Value && !p.IsArchived);

            if (project == null)
            {
                return new List<User>();
            }

            if (!CanManageProject(project))
            {
                return new List<User>();
            }

            var users = new List<User>();

            var owner = _db.Users.FirstOrDefault(u =>
                u.Id == project.OwnerId &&
                u.IsActive &&
                u.Role != "admin" &&
                u.Role != "director");

            if (owner != null)
            {
                users.Add(owner);
            }

            users.AddRange(project.Members
                .Where(m =>
                    m.User != null &&
                    m.User.IsActive &&
                    m.User.Role != "admin" &&
                    m.User.Role != "director")
                .Select(m => m.User!));

            return users
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .OrderBy(u => u.Name)
                .ToList();
        }

        private List<string> BuildTaskChangeHistory(
            TaskItem task,
            string newTitle,
            string newDescription,
            string newPriority,
            string newStatus,
            DateTime? newStartDate,
            DateTime? newDueDate,
            int newSpentHours,
            int? newProjectId,
            int? newAssigneeUserId)
        {
            var actions = new List<string>();

            AddChange(actions, "Название", task.Title, newTitle);
            AddChange(actions, "Описание", ShortText(task.Description), ShortText(newDescription));
            AddChange(actions, "Проект", GetProjectName(task.ProjectId), GetProjectName(newProjectId));
            AddChange(actions, "Исполнитель", GetUserName(task.AssigneeUserId), GetUserName(newAssigneeUserId));
            AddChange(actions, "Приоритет", FormatPriority(task.Priority), FormatPriority(newPriority));
            AddChange(actions, "Статус", FormatStatus(task.Status), FormatStatus(newStatus));
            AddChange(actions, "Дата начала", FormatDate(task.StartDate), FormatDate(newStartDate));
            AddChange(actions, "Срок выполнения", FormatDate(task.DueDate), FormatDate(newDueDate));
            AddChange(actions, "Затрачено часов", FormatHours(task.SpentHours), FormatHours(newSpentHours));

            return actions;
        }

        private static void AddChange(List<string> actions, string field, string oldValue, string newValue)
        {
            oldValue = string.IsNullOrWhiteSpace(oldValue) ? "—" : oldValue;
            newValue = string.IsNullOrWhiteSpace(newValue) ? "—" : newValue;

            if (oldValue != newValue)
            {
                actions.Add($"{field}: {oldValue} → {newValue}");
            }
        }

        private static string ShortText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "—";
            }

            var text = value.Trim();

            if (text.Length <= 80)
            {
                return text;
            }

            return text.Substring(0, 80) + "...";
        }

        private string GetProjectName(int? projectId)
        {
            if (!projectId.HasValue)
            {
                return "—";
            }

            return _db.Projects
                .Where(p => p.Id == projectId.Value)
                .Select(p => p.Name)
                .FirstOrDefault() ?? "—";
        }

        private string GetUserName(int? userId)
        {
            if (!userId.HasValue)
            {
                return "—";
            }

            return _db.Users
                .Where(u => u.Id == userId.Value)
                .Select(u => u.Name)
                .FirstOrDefault() ?? "—";
        }

        private static string FormatPriority(string? value)
        {
            return value switch
            {
                "high" => "Высокий",
                "medium" => "Средний",
                _ => "Низкий"
            };
        }

        private static string FormatStatus(string? value)
        {
            return value == "done" ? "Завершена" : "Активна";
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("dd.MM.yyyy") : "—";
        }

        private static string FormatHours(int value)
        {
            return $"{value} ч";
        }

        private static string NormalizePriority(string? value)
        {
            if (value == "high") return "high";
            if (value == "medium") return "medium";
            return "low";
        }

        private static string NormalizeStatus(string? value)
        {
            return value == "done" ? "done" : "active";
        }
    }
}
