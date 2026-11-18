using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class NotificationsModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public NotificationsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<NotificationItem> Items { get; private set; } = new();

        public int OverdueCount { get; private set; }
        public int TodayCount { get; private set; }
        public int WeekCount { get; private set; }
        public int ActivityCount { get; private set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            var visibleProjectsQuery = _db.Projects
                .Include(p => p.Members)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (!IsAdmin && CurrentUserId.HasValue)
            {
                var userId = CurrentUserId.Value;

                visibleProjectsQuery = visibleProjectsQuery.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m => m.UserId == userId));
            }

            var visibleProjectIds = visibleProjectsQuery
                .Select(p => p.Id)
                .ToList();

            var tasks = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssigneeUser)
                .Where(t =>
                    t.ProjectId.HasValue &&
                    visibleProjectIds.Contains(t.ProjectId.Value) &&
                    t.Status != "done" &&
                    t.DueDate.HasValue)
                .ToList();

            var today = DateTime.Today;
            var weekEnd = today.AddDays(7);

            foreach (var task in tasks.Where(t => t.DueDate!.Value.Date < today).OrderBy(t => t.DueDate))
            {
                Items.Add(new NotificationItem
                {
                    Title = "Просрочена задача",
                    Text = $"{task.Title} · {task.ProjectTitle} · исполнитель: {task.AssigneeName}",
                    Date = task.DueDate!.Value,
                    DateText = task.DueDateText,
                    Badge = "Просрочено",
                    CssClass = "danger",
                    Link = $"/TaskDetails?id={task.Id}"
                });
            }

            foreach (var task in tasks.Where(t => t.DueDate!.Value.Date == today).OrderBy(t => t.Title))
            {
                Items.Add(new NotificationItem
                {
                    Title = "Дедлайн сегодня",
                    Text = $"{task.Title} · {task.ProjectTitle} · исполнитель: {task.AssigneeName}",
                    Date = task.DueDate!.Value,
                    DateText = task.DueDateText,
                    Badge = "Сегодня",
                    CssClass = "warning",
                    Link = $"/TaskDetails?id={task.Id}"
                });
            }

            foreach (var task in tasks.Where(t => t.DueDate!.Value.Date > today && t.DueDate!.Value.Date <= weekEnd).OrderBy(t => t.DueDate))
            {
                Items.Add(new NotificationItem
                {
                    Title = "Скоро дедлайн",
                    Text = $"{task.Title} · {task.ProjectTitle} · исполнитель: {task.AssigneeName}",
                    Date = task.DueDate!.Value,
                    DateText = task.DueDateText,
                    Badge = "До 7 дней",
                    CssClass = "info",
                    Link = $"/TaskDetails?id={task.Id}"
                });
            }

            var recentHistory = _db.TaskHistories
                .Where(h => _db.Tasks.Any(t =>
                    t.Id == h.TaskItemId &&
                    t.ProjectId.HasValue &&
                    visibleProjectIds.Contains(t.ProjectId.Value)))
                .OrderByDescending(h => h.CreatedAt)
                .Take(8)
                .ToList();

            foreach (var history in recentHistory)
            {
                Items.Add(new NotificationItem
                {
                    Title = history.Action,
                    Text = $"{history.TaskTitle} · {history.UserName}",
                    Date = history.CreatedAt,
                    DateText = history.CreatedAtText,
                    Badge = "История",
                    CssClass = "muted",
                    Link = $"/TaskHistory?taskId={history.TaskItemId}"
                });
            }

            OverdueCount = tasks.Count(t => t.DueDate!.Value.Date < today);
            TodayCount = tasks.Count(t => t.DueDate!.Value.Date == today);
            WeekCount = tasks.Count(t => t.DueDate!.Value.Date > today && t.DueDate!.Value.Date <= weekEnd);
            ActivityCount = recentHistory.Count;

            Items = Items
                .OrderBy(i => i.CssClass == "danger" ? 0 : i.CssClass == "warning" ? 1 : i.CssClass == "info" ? 2 : 3)
                .ThenBy(i => i.Date)
                .ToList();

            return Page();
        }
    }

    public class NotificationItem
    {
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public string DateText { get; set; } = "";
        public string Badge { get; set; } = "";
        public string CssClass { get; set; } = "";
        public string Link { get; set; } = "";
        public DateTime Date { get; set; }
    }
}