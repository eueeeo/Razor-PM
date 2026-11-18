using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace razorpm.Pages
{
    public class TaskDetailsModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public TaskDetailsModel(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public TaskItem? Task { get; private set; }
        public List<TaskComment> Comments { get; private set; } = new();
        public List<TaskAttachment> Attachments { get; private set; } = new();
        public List<TaskHistory> History { get; private set; } = new();

        [BindProperty]
        public string NewComment { get; set; } = "";

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        public string Error { get; set; } = "";

        public bool CanManageCurrentTask { get; private set; }
        public bool CanChangeStatusCurrentTask { get; private set; }

        public IActionResult OnGet(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadTask(id))
            {
                return RedirectToPage("/Tasks");
            }

            return Page();
        }

        public IActionResult OnPostAddComment(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadTask(id))
            {
                return RedirectToPage("/Tasks");
            }

            if (string.IsNullOrWhiteSpace(NewComment))
            {
                Error = "Введите комментарий";
                return Page();
            }

            _db.TaskComments.Add(new TaskComment
            {
                TaskItemId = id,
                AuthorName = CurrentName,
                Text = NewComment.Trim(),
                CreatedAt = DateTime.Now
            });

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = id,
                TaskTitle = Task!.Title,
                Action = "Добавлен комментарий",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskDetails", new { id });
        }

        public IActionResult OnPostUploadFile(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadTask(id))
            {
                return RedirectToPage("/Tasks");
            }

            if (UploadFile == null || UploadFile.Length == 0)
            {
                Error = "Выберите файл";
                return Page();
            }

            if (UploadFile.Length > 5 * 1024 * 1024)
            {
                Error = "Файл слишком большой";
                return Page();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var ext = Path.GetExtension(UploadFile.FileName);
            var safeName = $"task_{id}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsFolder, safeName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                UploadFile.CopyTo(stream);
            }

            _db.TaskAttachments.Add(new TaskAttachment
            {
                TaskItemId = id,
                FileName = UploadFile.FileName,
                FilePath = "/uploads/" + safeName,
                AuthorName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = id,
                TaskTitle = Task!.Title,
                Action = "Добавлено вложение",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskDetails", new { id });
        }

        public IActionResult OnPostSetStatus(int id, string? value)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadTask(id))
            {
                return RedirectToPage("/Tasks");
            }

            if (!CanChangeStatusCurrentTask)
            {
                return RedirectToPage("/Tasks");
            }

            var oldStatus = Task!.Status;

            string newStatus;

            if (value == "done" || value == "active")
            {
                newStatus = value;
            }
            else
            {
                newStatus = Task.Status == "done" ? "active" : "done";
            }

            if (Task.Status != newStatus)
            {
                Task.Status = newStatus;

                _db.TaskHistories.Add(new TaskHistory
                {
                    TaskItemId = Task.Id,
                    TaskTitle = Task.Title,
                    Action = oldStatus == "done"
                        ? "Статус изменен: завершена → активна"
                        : "Статус изменен: активна → завершена",
                    UserName = CurrentName,
                    CreatedAt = DateTime.Now
                });

                SyncProjectStatus(_db, Task.ProjectId);
                _db.SaveChanges();
            }

            return RedirectToPage("/TaskDetails", new { id });
        }

        public IActionResult OnPostDelete(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadTask(id))
            {
                return RedirectToPage("/Tasks");
            }

            if (!CanManageCurrentTask)
            {
                return RedirectToPage("/Tasks");
            }

            var projectIdToReturn = Task!.ProjectId;
            var projectIdToSync = Task.ProjectId;

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = Task.Id,
                TaskTitle = Task.Title,
                Action = "Удаление задачи",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            DeleteTaskWithRelatedData(Task);
            _db.SaveChanges();

            SyncProjectStatus(_db, projectIdToSync);
            _db.SaveChanges();

            if (projectIdToReturn.HasValue)
            {
                return RedirectToPage("/ProjectDetails", new { id = projectIdToReturn.Value });
            }

            return RedirectToPage("/Tasks");
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

        private bool LoadTask(int id)
        {
            Task = _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Members)
                .Include(t => t.AssigneeUser)
                .FirstOrDefault(t => t.Id == id);

            if (Task == null)
            {
                return false;
            }

            if (!CanViewTask(Task))
            {
                return false;
            }

            CanManageCurrentTask = CanManageTask(Task);
            CanChangeStatusCurrentTask = CanChangeOwnTaskStatus(Task);

            Comments = _db.TaskComments
                .Where(c => c.TaskItemId == id)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            Attachments = _db.TaskAttachments
                .Where(a => a.TaskItemId == id)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            History = _db.TaskHistories
                .Where(h => h.TaskItemId == id)
                .OrderByDescending(h => h.CreatedAt)
                .Take(10)
                .ToList();

            return true;
        }
    }
}