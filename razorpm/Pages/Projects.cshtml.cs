using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class ProjectsModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ProjectsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<Project> Projects { get; private set; } = new();
        public List<SelectListItem> OwnerOptions { get; private set; } = new();

        public bool CanCreateNewProject => CanCreateProject();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? status { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ownerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? archive { get; set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            LoadOwners();

            var query = _db.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Members)
                .AsQueryable();

            if (IsAdmin)
            {
                query = query.Where(p => false);
            }
            else if (IsDirector)
            {
            }
            else if (CurrentUserId.HasValue)
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

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p =>
                    p.Name.Contains(q) ||
                    p.OwnerName.Contains(q));
            }

            if (status == "active" || status == "done")
            {
                query = query.Where(p => p.Status == status);
            }

            if (archive == "archived")
            {
                query = query.Where(p => p.IsArchived);
            }
            else if (archive == "active")
            {
                query = query.Where(p => !p.IsArchived);
            }

            if (ownerId.HasValue)
            {
                query = query.Where(p => p.OwnerId == ownerId.Value);
            }

            Projects = query
                .OrderBy(p => p.IsArchived)
                .ThenBy(p => p.Name)
                .ToList();

            return Page();
        }

        public bool CanManage(Project project)
        {
            return CanManageProject(project);
        }

        public bool IsOwner(Project project)
        {
            return CurrentUserId.HasValue && project.OwnerId == CurrentUserId.Value;
        }

        public string GetMyRole(Project project)
        {
            if (IsDirector) return "Руководитель";
            if (IsOwner(project)) return "Владелец";
            return "Участник";
        }

        public IActionResult OnPostToggleArchive(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            var project = _db.Projects.FirstOrDefault(p => p.Id == id);
            if (project == null)
            {
                return RedirectToPage(new { q, status, ownerId, archive });
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage(new { q, status, ownerId, archive });
            }

            project.IsArchived = !project.IsArchived;
            _db.SaveChanges();

            return RedirectToPage(new { q, status, ownerId, archive });
        }

        public IActionResult OnPostDelete(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            var project = _db.Projects.FirstOrDefault(p => p.Id == id);
            if (project == null)
            {
                return RedirectToPage(new { q, status, ownerId, archive });
            }

            if (!CanManageProject(project))
            {
                return RedirectToPage(new { q, status, ownerId, archive });
            }

            _db.Projects.Remove(project);
            _db.SaveChanges();

            return RedirectToPage(new { q, status, ownerId, archive });
        }

        private void LoadOwners()
        {
            if (!CurrentUserId.HasValue)
            {
                OwnerOptions = new List<SelectListItem>();
                return;
            }

            if (IsAdmin)
            {
                OwnerOptions = new List<SelectListItem>();
                return;
            }

            var ownersQuery = _db.Users.AsQueryable();

            if (!IsDirector)
            {
                var userId = CurrentUserId.Value;

                var ownerIds = _db.Projects
                    .Include(p => p.Members)
                    .Where(p =>
                        p.OwnerId == userId ||
                        p.Members.Any(m => m.UserId == userId))
                    .Select(p => p.OwnerId)
                    .Distinct()
                    .ToList();

                ownersQuery = ownersQuery.Where(u => ownerIds.Contains(u.Id));
            }

            OwnerOptions = ownersQuery
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Login)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Name} ({u.Login})"
                })
                .ToList();
        }
    }
}
