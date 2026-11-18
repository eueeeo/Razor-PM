using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using razorpm.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace razorpm.Pages
{
    public class TaskCreateModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;

        public TaskCreateModel(AppDbContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [BindProperty] public string Title { get; set; } = "";
        [BindProperty] public string Description { get; set; } = "";
        [BindProperty] public string Priority { get; set; } = "low";
        [BindProperty] public string Status { get; set; } = "active";
        [BindProperty] public DateTime? StartDate { get; set; }
        [BindProperty] public DateTime? DueDate { get; set; }
        [BindProperty] public int? SpentHours { get; set; }
        [BindProperty] public int? ProjectId { get; set; }
        [BindProperty] public int? AssigneeUserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? date { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? projectId { get; set; }

        public string Error { get; set; } = "";

        public List<SelectListItem> ProjectOptions { get; private set; } = new();
        public List<SelectListItem> AssigneeOptions { get; private set; } = new();

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanCreateTask())
            {
                return RedirectToPage("/Tasks");
            }

            LoadProjects();

            if (projectId.HasValue &&
                ProjectOptions.Any(p => p.Value == projectId.Value.ToString()))
            {
                ProjectId = projectId.Value;
            }

            if (DateTime.TryParse(date, out var selectedDate))
            {
                StartDate = selectedDate.Date;
                DueDate = selectedDate.Date;
            }
            else
            {
                StartDate = DateTime.Today;
            }

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

        public async Task<IActionResult> OnPostAsync()
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

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == ProjectId.Value && !p.IsArchived);

            if (project == null)
            {
                Error = "Проект не найден или находится в архиве";
                return Page();
            }

            if (!CanUseProjectForTaskCreate(project))
            {
                Error = "Нет прав назначать задачи в этом проекте";
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
                u.Role != "admin");

            if (!assigneeIsActive)
            {
                Error = "Выбранный исполнитель не найден или отключён";
                return Page();
            }

            var task = new TaskItem
            {
                Title = Title,
                Description = Description,
                Priority = Priority,
                Status = Status,
                AssigneeUserId = AssigneeUserId,
                StartDate = StartDate,
                DueDate = DueDate,
                SpentHours = SpentHours.Value,
                ProjectId = ProjectId
            };

            _db.Tasks.Add(task);
            _db.SaveChanges();

            SyncProjectStatus(_db, task.ProjectId);
            _db.SaveChanges();

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = task.Id,
                TaskTitle = task.Title,
                Action = "Создание задачи",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            var assignee = _db.Users.FirstOrDefault(u => u.Id == task.AssigneeUserId);

            if (assignee != null && !string.IsNullOrWhiteSpace(assignee.Email))
            {
                var subject = "Вам назначена новая задача";

                var body =
$@"Здравствуйте, {assignee.Name}!

Вам назначена новая задача в системе RazorPM.

Название: {task.Title}
Проект: {project.Name}
Приоритет: {task.PriorityRu}
Срок: {task.DueDateText}

Пожалуйста, проверьте задачу в системе.";

                try
                {
                    await _emailService.SendAsync(assignee.Email, subject, body);
                }
                catch
                {
                }
            }

            return RedirectToPage("/Tasks");
        }

        private bool CanCreateTask()
        {
            return IsDirector || IsManager;
        }

        private bool CanUseProjectForTaskCreate(Project project)
        {
            if (!CurrentUserId.HasValue)
            {
                return false;
            }

            if (IsDirector)
            {
                return true;
            }

            if (IsManager)
            {
                var userId = CurrentUserId.Value;

                return project.OwnerId == userId ||
                       project.Members.Any(m => m.UserId == userId);
            }

            return false;
        }

        private void LoadProjects()
        {
            var query = _db.Projects
                .Include(p => p.Members)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (IsDirector)
            {
            }
            else if (IsManager && CurrentUserId.HasValue)
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

            if (!CanUseProjectForTaskCreate(project))
            {
                return new List<User>();
            }

            var users = new List<User>();

            var owner = _db.Users.FirstOrDefault(u =>
                u.Id == project.OwnerId &&
                u.IsActive &&
                u.Role != "admin");

            if (owner != null)
            {
                users.Add(owner);
            }

            users.AddRange(project.Members
                .Where(m =>
                    m.User != null &&
                    m.User.IsActive &&
                    m.User.Role != "admin")
                .Select(m => m.User!));

            return users
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .OrderBy(u => u.Name)
                .ToList();
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
