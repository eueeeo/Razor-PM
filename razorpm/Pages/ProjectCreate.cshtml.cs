using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class ProjectCreateModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ProjectCreateModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string Name { get; set; } = "";

        [BindProperty]
        public string Status { get; set; } = "active";

        public string Error { get; set; } = "";

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanCreateProject())
            {
                return RedirectToPage("/Projects");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanCreateProject())
            {
                return RedirectToPage("/Projects");
            }

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            Name = (Name ?? "").Trim();
            Status = NormalizeStatus(Status);

            if (string.IsNullOrWhiteSpace(Name))
            {
                Error = "Введите название проекта";
                return Page();
            }

            if (Name.Length < 3)
            {
                Error = "Название проекта должно быть не короче 3 символов";
                return Page();
            }

            if (Name.Length > 100)
            {
                Error = "Название проекта не должно быть длиннее 100 символов";
                return Page();
            }

            var owner = _db.Users.FirstOrDefault(u =>
                u.Id == CurrentUserId.Value &&
                u.IsActive);

            if (owner == null)
            {
                Error = "Пользователь не найден или отключён";
                return Page();
            }

            var sameNameExists = _db.Projects.Any(p =>
                p.OwnerId == owner.Id &&
                p.Name.ToLower() == Name.ToLower());

            if (sameNameExists)
            {
                Error = "У вас уже есть проект с таким названием";
                return Page();
            }

            var project = new Project
            {
                Name = Name,
                Status = Status,
                IsArchived = false,
                OwnerId = owner.Id,
                OwnerName = owner.Name
            };

            _db.Projects.Add(project);
            _db.SaveChanges();

            return RedirectToPage("/ProjectDetails", new { id = project.Id });
        }

        private static string NormalizeStatus(string? value)
        {
            return value == "done" ? "done" : "active";
        }
    }
}