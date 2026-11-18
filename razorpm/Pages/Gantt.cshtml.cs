using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace razorpm.Pages
{
    public class GanttModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public GanttModel(AppDbContext db)
        {
            _db = db;
        }

        public List<GanttProjectGroup> Groups { get; private set; } = new();
        public List<GanttDayHeader> Headers { get; private set; } = new();
        public List<SelectListItem> ProjectOptions { get; private set; } = new();

        public DateTime ScaleStart { get; private set; }
        public DateTime ScaleEnd { get; private set; }
        public int TotalDays { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? projectId { get; set; }

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

            var userId = CurrentUserId.Value;

            var projectsQuery = _db.Projects
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (IsDirector)
            {
            }
            else if (IsManager)
            {
                projectsQuery = projectsQuery.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m => m.UserId == userId));
            }
            else
            {
                projectsQuery = projectsQuery.Where(p =>
                    p.Tasks.Any(t => t.AssigneeUserId == userId));
            }

            var projects = projectsQuery
                .OrderBy(p => p.Name)
                .ToList();

            var allowedProjectIds = projects
                .Select(p => p.Id)
                .ToList();

            ProjectOptions = projects
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToList();

            if (projectId.HasValue && !allowedProjectIds.Contains(projectId.Value))
            {
                projectId = null;
            }

            var tasksQuery = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssigneeUser)
                .Where(t =>
                    t.StartDate.HasValue &&
                    t.DueDate.HasValue &&
                    t.ProjectId.HasValue &&
                    allowedProjectIds.Contains(t.ProjectId.Value))
                .AsQueryable();

            if (IsExecutor)
            {
                tasksQuery = tasksQuery.Where(t => t.AssigneeUserId == userId);
            }

            if (projectId.HasValue)
            {
                tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId.Value);
            }

            var tasks = tasksQuery
                .OrderBy(t => t.ProjectId)
                .ThenBy(t => t.StartDate)
                .ThenBy(t => t.Title)
                .ToList();

            if (tasks.Count == 0)
            {
                ScaleStart = DateTime.Today;
                ScaleEnd = DateTime.Today.AddDays(6);
                TotalDays = 7;

                BuildHeaders();

                return Page();
            }

            ScaleStart = tasks.Min(t => t.StartDate!.Value).Date;
            ScaleEnd = tasks.Max(t => t.DueDate!.Value).Date;
            TotalDays = (ScaleEnd - ScaleStart).Days + 1;

            BuildHeaders();

            Groups = tasks
                .GroupBy(t => new
                {
                    ProjectId = t.ProjectId ?? 0,
                    ProjectName = t.ProjectTitle
                })
                .Select(g => new GanttProjectGroup
                {
                    ProjectId = g.Key.ProjectId,
                    ProjectName = g.Key.ProjectName,
                    Items = g.Select(t =>
                    {
                        var start = t.StartDate!.Value.Date;
                        var end = t.DueDate!.Value.Date;

                        var offset = (start - ScaleStart).Days;
                        var length = (end - start).Days + 1;

                        return new GanttTaskRow
                        {
                            TaskId = t.Id,
                            Title = t.Title,
                            Assignee = t.AssigneeName,
                            Status = t.Status,
                            StatusRu = t.StatusRu,
                            StartText = t.StartDateText,
                            EndText = t.DueDateText,
                            IsOverdue = end < DateTime.Today && t.Status != "done",
                            OffsetPercent = TotalDays == 0 ? 0 : offset * 100.0 / TotalDays,
                            WidthPercent = TotalDays == 0 ? 0 : length * 100.0 / TotalDays
                        };
                    }).ToList()
                })
                .OrderBy(g => g.ProjectName)
                .ToList();

            return Page();
        }

        private void BuildHeaders()
        {
            Headers = new List<GanttDayHeader>();

            for (int i = 0; i < TotalDays; i++)
            {
                var date = ScaleStart.AddDays(i);

                Headers.Add(new GanttDayHeader
                {
                    Date = date,
                    DayNumber = date.Day.ToString(),
                    DayShort = date.ToString("dd.MM", new CultureInfo("ru-RU"))
                });
            }
        }
    }

    public class GanttProjectGroup
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public List<GanttTaskRow> Items { get; set; } = new();
    }

    public class GanttTaskRow
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = "";
        public string Assignee { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusRu { get; set; } = "";
        public string StartText { get; set; } = "";
        public string EndText { get; set; } = "";
        public double OffsetPercent { get; set; }
        public double WidthPercent { get; set; }
        public bool IsOverdue { get; set; }

        public string BarClass
        {
            get
            {
                if (IsOverdue) return "gantt-bar overdue";
                if (Status == "done") return "gantt-bar done";
                return "gantt-bar active";
            }
        }
    }

    public class GanttDayHeader
    {
        public DateTime Date { get; set; }
        public string DayNumber { get; set; } = "";
        public string DayShort { get; set; } = "";
    }
}
