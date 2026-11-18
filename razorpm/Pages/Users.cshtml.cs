using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using System.Collections.Generic;
using System.Linq;

namespace razorpm.Pages
{
    public class UsersModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public UsersModel(AppDbContext db)
        {
            _db = db;
        }

        public List<UserRow> Rows { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? role { get; set; }

        [TempData]
        public string? UserError { get; set; }

        [TempData]
        public string? UserMessage { get; set; }

        public bool IsDirectorMode => IsDirector && !IsAdmin;

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!IsAdmin && !IsDirector)
            {
                return RedirectToPage("/Index");
            }

            NormalizeFilters();
            LoadUsers();

            return Page();
        }

        public IActionResult OnPostChangeRole(int id, string newRole)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!IsAdmin && !IsDirector)
            {
                return RedirectToPage("/Index");
            }

            NormalizeFilters();

            if (id <= 0)
            {
                UserError = "Пользователь не выбран";
                return RedirectToPage(new { q, role });
            }

            var normalizedRole = NormalizeRole(newRole);

            if (normalizedRole == null)
            {
                UserError = "Некорректная роль пользователя";
                return RedirectToPage(new { q, role });
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                UserError = "Пользователь не найден";
                return RedirectToPage(new { q, role });
            }

            if (CurrentUserId.HasValue && user.Id == CurrentUserId.Value)
            {
                UserError = "Нельзя изменить роль своего аккаунта";
                return RedirectToPage(new { q, role });
            }

            if (IsDirector && !IsAdmin)
            {
                var targetIsAllowed = user.Role == "user" || user.Role == "manager";
                var newRoleIsAllowed = normalizedRole == "user" || normalizedRole == "manager";

                if (!targetIsAllowed || !newRoleIsAllowed)
                {
                    UserError = "Руководитель может назначать только ответственных и исполнителей";
                    return RedirectToPage(new { q, role });
                }
            }

            if (user.Role == normalizedRole)
            {
                UserMessage = "Эта роль уже установлена";
                return RedirectToPage(new { q, role });
            }

            if (IsAdmin && user.Role == "admin" && normalizedRole != "admin")
            {
                var activeAdminsAfterChange = _db.Users.Count(u =>
                    u.Id != user.Id &&
                    u.Role == "admin" &&
                    u.IsActive);

                if (activeAdminsAfterChange == 0)
                {
                    UserError = "Нельзя снять роль у последнего активного администратора";
                    return RedirectToPage(new { q, role });
                }
            }

            user.Role = normalizedRole;
            _db.SaveChanges();

            UserMessage = "Роль пользователя изменена";

            return RedirectToPage(new { q, role });
        }

        public IActionResult OnPostToggleActive(int id)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!IsAdmin)
            {
                return RedirectToPage("/Index");
            }

            NormalizeFilters();

            if (id <= 0)
            {
                UserError = "Пользователь не выбран";
                return RedirectToPage(new { q, role });
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                UserError = "Пользователь не найден";
                return RedirectToPage(new { q, role });
            }

            if (CurrentUserId.HasValue && user.Id == CurrentUserId.Value)
            {
                UserError = "Нельзя отключить свой аккаунт";
                return RedirectToPage(new { q, role });
            }

            if (user.IsActive && user.Role == "admin")
            {
                var activeAdminsAfterDisable = _db.Users.Count(u =>
                    u.Id != user.Id &&
                    u.Role == "admin" &&
                    u.IsActive);

                if (activeAdminsAfterDisable == 0)
                {
                    UserError = "Нельзя отключить последнего активного администратора";
                    return RedirectToPage(new { q, role });
                }
            }

            user.IsActive = !user.IsActive;
            _db.SaveChanges();

            UserMessage = user.IsActive
                ? "Пользователь включён"
                : "Пользователь отключён";

            return RedirectToPage(new { q, role });
        }

        public bool CanChangeRole(UserRow user)
        {
            if (user.IsCurrentUser)
            {
                return false;
            }

            if (IsAdmin)
            {
                return true;
            }

            if (IsDirector)
            {
                return user.Role == "user" || user.Role == "manager";
            }

            return false;
        }

        public bool CanToggleActive(UserRow user)
        {
            if (user.IsCurrentUser)
            {
                return false;
            }

            return IsAdmin;
        }

        private void LoadUsers()
        {
            var usersQuery = _db.Users.AsQueryable();

            if (IsDirector && !IsAdmin)
            {
                usersQuery = usersQuery.Where(u =>
                    u.Role == "user" ||
                    u.Role == "manager");
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();

                usersQuery = usersQuery.Where(u =>
                    u.Name.Contains(search) ||
                    u.Login.Contains(search) ||
                    u.Email.Contains(search));
            }

            if (IsKnownRole(role))
            {
                usersQuery = usersQuery.Where(u => u.Role == role);
            }

            var users = usersQuery
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ThenBy(u => u.Login)
                .ToList();

            var ownerProjectCounts = _db.Projects
                .GroupBy(p => p.OwnerId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count()
                })
                .ToDictionary(x => x.UserId, x => x.Count);

            var memberProjectCounts = _db.ProjectMembers
                .GroupBy(pm => pm.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count()
                })
                .ToDictionary(x => x.UserId, x => x.Count);

            var assignedTaskCounts = _db.Tasks
                .Where(t => t.AssigneeUserId.HasValue)
                .GroupBy(t => t.AssigneeUserId!.Value)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count()
                })
                .ToDictionary(x => x.UserId, x => x.Count);

            Rows = users
                .Select(u =>
                {
                    var normalizedRole = NormalizeRole(u.Role) ?? "user";

                    return new UserRow
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Login = u.Login,
                        Email = u.Email,
                        Role = normalizedRole,
                        RoleTitle = GetRoleTitle(normalizedRole),
                        Theme = string.IsNullOrWhiteSpace(u.Theme) ? "light" : u.Theme,
                        IsActive = u.IsActive,
                        OwnerProjectsCount = ownerProjectCounts.ContainsKey(u.Id) ? ownerProjectCounts[u.Id] : 0,
                        MemberProjectsCount = memberProjectCounts.ContainsKey(u.Id) ? memberProjectCounts[u.Id] : 0,
                        AssignedTasksCount = assignedTaskCounts.ContainsKey(u.Id) ? assignedTaskCounts[u.Id] : 0,
                        IsCurrentUser = CurrentUserId.HasValue && u.Id == CurrentUserId.Value
                    };
                })
                .ToList();
        }

        private void NormalizeFilters()
        {
            q = (q ?? "").Trim();

            if (IsDirector && !IsAdmin)
            {
                if (role != "manager" && role != "user")
                {
                    role = null;
                }

                return;
            }

            if (!IsKnownRole(role))
            {
                role = null;
            }
        }

        private static bool IsKnownRole(string? value)
        {
            return value == "admin" ||
                   value == "director" ||
                   value == "manager" ||
                   value == "user";
        }

        private static string? NormalizeRole(string? value)
        {
            if (value == "admin") return "admin";
            if (value == "director") return "director";
            if (value == "manager") return "manager";
            if (value == "user") return "user";

            return null;
        }

        public static string GetRoleTitle(string? value)
        {
            return value switch
            {
                "admin" => "Администратор",
                "director" => "Руководитель",
                "manager" => "Ответственный",
                _ => "Исполнитель"
            };
        }

        public class UserRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Login { get; set; } = "";
            public string Email { get; set; } = "";
            public string Role { get; set; } = "";
            public string RoleTitle { get; set; } = "";
            public string Theme { get; set; } = "";
            public bool IsActive { get; set; }
            public int OwnerProjectsCount { get; set; }
            public int MemberProjectsCount { get; set; }
            public int AssignedTasksCount { get; set; }
            public bool IsCurrentUser { get; set; }
        }
    }
}