using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class TaskHistoryModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public TaskHistoryModel(AppDbContext db)
        {
            _db = db;
        }

        public TaskItem? Task { get; private set; }
        public List<TaskHistory> History { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int taskId { get; set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadData())
            {
                return RedirectToPage("/Tasks");
            }

            return Page();
        }

        private bool LoadData()
        {
            Task = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .Include(t => t.AssigneeUser)
                .FirstOrDefault(t => t.Id == taskId);

            if (Task == null)
            {
                return false;
            }

            if (!CanViewTask(Task))
            {
                Task = null;
                return false;
            }

            History = _db.TaskHistories
                .Where(h => h.TaskItemId == taskId)
                .OrderByDescending(h => h.CreatedAt)
                .ToList();

            return true;
        }
    }
}