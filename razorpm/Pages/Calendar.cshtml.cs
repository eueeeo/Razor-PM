using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace razorpm.Pages
{
    public class CalendarModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public CalendarModel(AppDbContext db)
        {
            _db = db;
        }

        public int Year { get; private set; }
        public int Month { get; private set; }

        public int PrevMonth { get; private set; }
        public int PrevYear { get; private set; }
        public int NextMonth { get; private set; }
        public int NextYear { get; private set; }

        public string MonthTitle { get; private set; } = "";

        public DateTime SelectedDate { get; private set; }
        public List<CalendarDayItem> Days { get; private set; } = new();
        public List<TaskItem> DayTasks { get; private set; } = new();
        public List<SelectListItem> ProjectOptions { get; private set; } = new();

        public bool CanCreateNewTask => CanCreateTask();
        public bool CanUseProjectFilter => IsDirector || IsManager;

        [BindProperty(SupportsGet = true)]
        public int? y { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? m { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? d { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? projectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? archive { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? scope { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? status { get; set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (IsAdmin)
            {
                return RedirectToPage("/AdminDashboard");
            }

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            NormalizeFilters();

            var today = DateTime.Today;

            Year = y ?? today.Year;
            Month = m ?? today.Month;

            if (Month < 1) Month = 1;
            if (Month > 12) Month = 12;

            var firstDayOfMonth = new DateTime(Year, Month, 1);
            var monthEnd = firstDayOfMonth.AddMonths(1).AddDays(-1);

            PrevMonth = Month == 1 ? 12 : Month - 1;
            PrevYear = Month == 1 ? Year - 1 : Year;

            NextMonth = Month == 12 ? 1 : Month + 1;
            NextYear = Month == 12 ? Year + 1 : Year;

            MonthTitle = firstDayOfMonth.ToString("MMMM yyyy", new CultureInfo("ru-RU"));

            if (!DateTime.TryParse(d, out var parsedDate))
            {
                parsedDate = today;
            }

            if (parsedDate.Year != Year || parsedDate.Month != Month)
            {
                parsedDate = firstDayOfMonth;
            }

            SelectedDate = parsedDate.Date;

            var availableProjects = GetAvailableProjects();

            ProjectOptions = availableProjects
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToList();

            var allowedProjectIds = availableProjects
                .Select(p => p.Id)
                .ToList();

            if (projectId.HasValue && !allowedProjectIds.Contains(projectId.Value))
            {
                projectId = null;
            }

            var tasksQuery = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .Include(t => t.AssigneeUser)
                .Where(t =>
                    t.DueDate.HasValue &&
                    t.ProjectId.HasValue &&
                    allowedProjectIds.Contains(t.ProjectId.Value))
                .AsQueryable();

            var userId = CurrentUserId.Value;

            if (IsExecutor)
            {
                tasksQuery = tasksQuery.Where(t => t.AssigneeUserId == userId);
            }

            if (scope == "my")
            {
                tasksQuery = tasksQuery.Where(t => t.AssigneeUserId == userId);
            }

            if (scope == "today")
            {
                tasksQuery = tasksQuery.Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate.Value.Date == today);
            }

            if (scope == "overdue")
            {
                tasksQuery = tasksQuery.Where(t =>
                    t.Status != "done" &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value.Date < today);
            }

            if (status == "active")
            {
                tasksQuery = tasksQuery.Where(t => t.Status != "done");
            }
            else if (status == "done")
            {
                tasksQuery = tasksQuery.Where(t => t.Status == "done");
            }

            if (projectId.HasValue)
            {
                tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId.Value);
            }

            var monthTasks = tasksQuery
                .Where(t =>
                    t.DueDate!.Value.Date >= firstDayOfMonth.Date &&
                    t.DueDate!.Value.Date <= monthEnd.Date)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Title)
                .ToList();

            Days = new List<CalendarDayItem>();

            var daysInMonth = DateTime.DaysInMonth(Year, Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(Year, Month, day);

                var tasksForDay = monthTasks
                    .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == date.Date)
                    .OrderBy(t => t.Title)
                    .ToList();

                Days.Add(new CalendarDayItem
                {
                    Date = date,
                    Tasks = tasksForDay
                });
            }

            DayTasks = monthTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == SelectedDate.Date)
                .OrderBy(t => t.Title)
                .ToList();

            return Page();
        }

        private List<Project> GetAvailableProjects()
        {
            if (!CurrentUserId.HasValue)
            {
                return new List<Project>();
            }

            var userId = CurrentUserId.Value;

            var query = _db.Projects
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .AsQueryable();

            if (archive == "archived")
            {
                query = query.Where(p => p.IsArchived);
            }
            else
            {
                query = query.Where(p => !p.IsArchived);
            }

            if (IsDirector)
            {
            }
            else if (IsManager)
            {
                query = query.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(mb => mb.UserId == userId));
            }
            else
            {
                query = query.Where(p =>
                    p.Tasks.Any(t => t.AssigneeUserId == userId));
            }

            return query
                .OrderBy(p => p.Name)
                .ToList();
        }

        private void NormalizeFilters()
        {
            if (archive != "archived")
            {
                archive = "";
            }

            if (scope != "my" && scope != "today" && scope != "overdue")
            {
                scope = "";
            }

            if (status != "active" && status != "done")
            {
                status = "";
            }

            if (IsExecutor)
            {
                scope = "my";
            }
        }
    }

    public class CalendarDayItem
    {
        public DateTime Date { get; set; }
        public List<TaskItem> Tasks { get; set; } = new();
    }
}
