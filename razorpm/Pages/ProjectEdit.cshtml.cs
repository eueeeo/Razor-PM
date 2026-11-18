using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using System.Linq;

namespace razorpm.Pages
{
    public class ProjectEditModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ProjectEditModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = "";

        [BindProperty]
        public string Status { get; set; } = "active";

        public string Error { get; set; } = "";

        public IActionResult OnGet(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == id);

            if (project == null)
            {
                return RedirectToPage("/Projects");
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage("/Projects");
            }

            Id = project.Id;
            Name = project.Name ?? "";
            Status = NormalizeStatus(project.Status);

            return Page();
        }

        public IActionResult OnPost()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            Name = (Name ?? "").Trim();
            Status = NormalizeStatus(Status);

            var project = _db.Projects
                .Include(p => p.Members)
                .FirstOrDefault(p => p.Id == Id);

            if (project == null)
            {
                return RedirectToPage("/Projects");
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage("/Projects");
            }

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

            var sameNameExists = _db.Projects.Any(p =>
                p.Id != project.Id &&
                p.OwnerId == project.OwnerId &&
                p.Name.ToLower() == Name.ToLower());

            if (sameNameExists)
            {
                Error = "У этого владельца уже есть другой проект с таким названием";
                return Page();
            }

            project.Name = Name;
            project.Status = Status;

            _db.SaveChanges();

            return RedirectToPage("/ProjectDetails", new { id = project.Id });
        }

        private static string NormalizeStatus(string? value)
        {
            return value == "done" ? "done" : "active";
        }
    }
}