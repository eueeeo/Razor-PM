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
    public class TaskCommentsModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        private const long MaxFileSize = 5 * 1024 * 1024;
        private const int MaxCommentLength = 1000;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".png",
            ".jpg",
            ".jpeg",
            ".zip"
        };

        public TaskCommentsModel(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public TaskItem? Task { get; private set; }
        public List<TaskComment> Comments { get; private set; } = new();
        public List<TaskAttachment> Attachments { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int taskId { get; set; }

        [BindProperty]
        public string NewComment { get; set; } = "";

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        public string Error { get; set; } = "";

        public bool CanManageCurrentTask { get; private set; }

        public bool CanDeleteComment(TaskComment comment)
        {
            if (CanManageCurrentTask)
            {
                return true;
            }

            return string.Equals(comment.AuthorName, CurrentName, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanDeleteAttachment(TaskAttachment attachment)
        {
            if (CanManageCurrentTask)
            {
                return true;
            }

            return string.Equals(attachment.AuthorName, CurrentName, StringComparison.OrdinalIgnoreCase);
        }

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

        public IActionResult OnPostAddComment()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadData())
            {
                return RedirectToPage("/Tasks");
            }

            NewComment = (NewComment ?? "").Trim();

            if (string.IsNullOrWhiteSpace(NewComment))
            {
                Error = "Введите комментарий";
                return Page();
            }

            if (NewComment.Length > MaxCommentLength)
            {
                Error = $"Комментарий не должен быть длиннее {MaxCommentLength} символов";
                return Page();
            }

            _db.TaskComments.Add(new TaskComment
            {
                TaskItemId = taskId,
                AuthorName = CurrentName,
                Text = NewComment,
                CreatedAt = DateTime.Now
            });

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = taskId,
                TaskTitle = Task!.Title,
                Action = "Добавлен комментарий",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskComments", new { taskId });
        }

        public IActionResult OnPostUpload()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadData())
            {
                return RedirectToPage("/Tasks");
            }

            if (UploadFile == null || UploadFile.Length == 0)
            {
                Error = "Выберите файл";
                return Page();
            }

            if (UploadFile.Length > MaxFileSize)
            {
                Error = "Файл слишком большой. Максимальный размер — 5 МБ";
                return Page();
            }

            var originalFileName = Path.GetFileName(UploadFile.FileName ?? "");

            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                Error = "Некорректное имя файла";
                return Page();
            }

            if (originalFileName.Length > 200)
            {
                Error = "Имя файла слишком длинное";
                return Page();
            }

            var ext = Path.GetExtension(originalFileName);

            if (string.IsNullOrWhiteSpace(ext))
            {
                Error = "Файл должен иметь расширение";
                return Page();
            }

            if (!AllowedExtensions.Contains(ext))
            {
                Error = "Недопустимый тип файла. Можно загружать: txt, pdf, doc, docx, xls, xlsx, png, jpg, jpeg, zip";
                return Page();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var safeName = $"task_{taskId}_{Guid.NewGuid():N}{ext.ToLower()}";
            var fullPath = Path.Combine(uploadsFolder, safeName);

            using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                UploadFile.CopyTo(stream);
            }

            _db.TaskAttachments.Add(new TaskAttachment
            {
                TaskItemId = taskId,
                FileName = originalFileName,
                FilePath = "/uploads/" + safeName,
                AuthorName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = taskId,
                TaskTitle = Task!.Title,
                Action = "Добавлено вложение",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskComments", new { taskId });
        }

        public IActionResult OnPostDeleteComment(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadData())
            {
                return RedirectToPage("/Tasks");
            }

            var comment = _db.TaskComments
                .FirstOrDefault(c => c.Id == id && c.TaskItemId == taskId);

            if (comment == null)
            {
                return RedirectToPage("/TaskComments", new { taskId });
            }

            if (!CanDeleteComment(comment))
            {
                Error = "Нет прав на удаление этого комментария";
                return Page();
            }

            _db.TaskComments.Remove(comment);

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = taskId,
                TaskTitle = Task!.Title,
                Action = "Удалён комментарий",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskComments", new { taskId });
        }

        public IActionResult OnPostDeleteAttachment(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!LoadData())
            {
                return RedirectToPage("/Tasks");
            }

            var file = _db.TaskAttachments
                .FirstOrDefault(a => a.Id == id && a.TaskItemId == taskId);

            if (file == null)
            {
                return RedirectToPage("/TaskComments", new { taskId });
            }

            if (!CanDeleteAttachment(file))
            {
                Error = "Нет прав на удаление этого файла";
                return Page();
            }

            DeletePhysicalFile(file.FilePath);

            _db.TaskAttachments.Remove(file);

            _db.TaskHistories.Add(new TaskHistory
            {
                TaskItemId = taskId,
                TaskTitle = Task!.Title,
                Action = "Удалено вложение",
                UserName = CurrentName,
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();

            return RedirectToPage("/TaskComments", new { taskId });
        }

        private bool LoadData()
        {
            if (taskId <= 0)
            {
                return false;
            }

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

            CanManageCurrentTask = CanManageTask(Task);

            Comments = _db.TaskComments
                .Where(c => c.TaskItemId == taskId)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            Attachments = _db.TaskAttachments
                .Where(a => a.TaskItemId == taskId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return true;
        }

        private void DeletePhysicalFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var normalizedPath = filePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());

            if (!normalizedPath.StartsWith("uploads" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullPath = Path.Combine(_env.WebRootPath, normalizedPath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}