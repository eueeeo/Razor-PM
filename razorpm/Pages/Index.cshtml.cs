using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public string DashboardTitle { get; private set; } = "Главная";
        public string DashboardDescription { get; private set; } = "";

        public int ProjectsTotal { get; private set; }
        public int ActiveProjectsTotal { get; private set; }
        public int TasksTotal { get; private set; }
        public int TasksActive { get; private set; }
        public int TasksDone { get; private set; }
        public int TasksOverdue { get; private set; }
        public int TasksToday { get; private set; }

        public int TasksProgressPercent =>
            TasksTotal == 0 ? 0 : (int)Math.Round((double)TasksDone / TasksTotal * 100);

        public List<Project> LastProjects { get; private set; } = new();
        public List<TaskItem> LastTasks { get; private set; } = new();
        public List<TaskItem> TodayTasks { get; private set; } = new();
        public List<TaskItem> UpcomingTasks { get; private set; } = new();
        public List<TaskItem> OverdueTasks { get; private set; } = new();
        public List<TaskHistory> RecentHistory { get; private set; } = new();

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (IsAdmin)
            {
                return RedirectToPage("/AdminDashboard");
            }

            LoadDashboard();

            return Page();
        }

        private void LoadDashboard()
        {
            var today = DateTime.Today;

            if (!CurrentUserId.HasValue)
            {
                return;
            }

            var userId = CurrentUserId.Value;

            if (IsDirector)
            {
                DashboardTitle = "Главная руководителя";
                DashboardDescription = "Общая рабочая картина по проектам и задачам";
            }
            else if (IsManager)
            {
                DashboardTitle = "Главная ответственного";
                DashboardDescription = "Ваши проекты, задачи и ближайшие сроки";
            }
            else
            {
                DashboardTitle = "Главная исполнителя";
                DashboardDescription = "Ваши назначенные задачи и сроки";
            }

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
                .OrderByDescending(p => p.Id)
                .ToList();

            var projectIds = projects.Select(p => p.Id).ToList();

            var tasksQuery = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssigneeUser)
                .Where(t =>
                    t.ProjectId.HasValue &&
                    projectIds.Contains(t.ProjectId.Value))
                .AsQueryable();

            if (IsExecutor)
            {
                tasksQuery = tasksQuery.Where(t => t.AssigneeUserId == userId);
            }

            var tasks = tasksQuery.ToList();

            ProjectsTotal = projects.Count;
            ActiveProjectsTotal = projects.Count(p => p.Status != "done");

            TasksTotal = tasks.Count;
            TasksActive = tasks.Count(t => t.Status != "done");
            TasksDone = tasks.Count(t => t.Status == "done");

            TasksOverdue = tasks.Count(t =>
                t.Status != "done" &&
                t.DueDate.HasValue &&
                t.DueDate.Value.Date < today);

            TasksToday = tasks.Count(t =>
                t.Status != "done" &&
                t.DueDate.HasValue &&
                t.DueDate.Value.Date == today);

            LastProjects = projects
                .Take(5)
                .ToList();

            LastTasks = tasks
                .OrderByDescending(t => t.Id)
                .Take(5)
                .ToList();

            TodayTasks = tasks
                .Where(t =>
                    t.Status != "done" &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value.Date == today)
                .OrderBy(t => t.Priority == "high" ? 0 : t.Priority == "medium" ? 1 : 2)
                .ThenBy(t => t.Title)
                .Take(5)
                .ToList();

            UpcomingTasks = tasks
                .Where(t =>
                    t.Status != "done" &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value.Date >= today)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Title)
                .Take(5)
                .ToList();

            OverdueTasks = tasks
                .Where(t =>
                    t.Status != "done" &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value.Date < today)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Title)
                .Take(5)
                .ToList();

            var taskIds = tasks.Select(t => t.Id).ToList();

            RecentHistory = _db.TaskHistories
                .Where(h => taskIds.Contains(h.TaskItemId))
                .OrderByDescending(h => h.CreatedAt)
                .Take(8)
                .ToList();
        }
    }
}
