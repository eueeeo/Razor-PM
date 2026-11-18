using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Helpers;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class SettingsModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public SettingsModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string Name { get; set; } = "";

        [BindProperty]
        public string Login { get; set; } = "";

        [BindProperty]
        public string Theme { get; set; } = "light";

        public string Error { get; set; } = "";
        public string Success { get; set; } = "";

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == CurrentUserId.Value);

            if (user == null || !user.IsActive)
            {
                return RedirectToPage("/Login");
            }

            Name = user.Name ?? "";
            Login = user.Login ?? "";
            Theme = NormalizeTheme(user.Theme);

            return Page();
        }

        public IActionResult OnPost()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == CurrentUserId.Value);

            if (user == null || !user.IsActive)
            {
                return RedirectToPage("/Login");
            }

            Name = (Name ?? "").Trim();
            Login = (Login ?? "").Trim();
            Theme = (Theme ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Name))
            {
                Error = "Введите имя";
                return Page();
            }

            if (Name.Length < 2)
            {
                Error = "Имя должно быть не короче 2 символов";
                return Page();
            }

            if (Name.Length > 60)
            {
                Error = "Имя не должно быть длиннее 60 символов";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Login))
            {
                Error = "Введите логин";
                return Page();
            }

            if (Login.Length < 3)
            {
                Error = "Логин должен быть не короче 3 символов";
                return Page();
            }

            if (Login.Length > 30)
            {
                Error = "Логин не должен быть длиннее 30 символов";
                return Page();
            }

            if (Login.Any(char.IsWhiteSpace))
            {
                Error = "Логин не должен содержать пробелы";
                return Page();
            }

            if (!Login.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
            {
                Error = "Логин может содержать только буквы, цифры, точку, дефис и подчёркивание";
                return Page();
            }

            if (Theme != "light" && Theme != "dark")
            {
                Error = "Выберите корректную тему";
                Theme = NormalizeTheme(Theme);
                return Page();
            }

            var loginForSearch = Login.ToLower();

            var loginExists = _db.Users.Any(u =>
                u.Id != user.Id &&
                u.Login.ToLower() == loginForSearch);

            if (loginExists)
            {
                Error = "Такой логин уже занят";
                return Page();
            }

            user.Name = Name;
            user.Login = Login;
            user.Theme = Theme;

            _db.SaveChanges();

            HttpContext.Session.SetString(SessionKeys.Name, user.Name);
            HttpContext.Session.SetString(SessionKeys.Login, user.Login);
            HttpContext.Session.SetString(SessionKeys.Theme, user.Theme);

            Success = "Настройки сохранены";

            return Page();
        }

        private static string NormalizeTheme(string? value)
        {
            return value == "dark" ? "dark" : "light";
        }
    }
}