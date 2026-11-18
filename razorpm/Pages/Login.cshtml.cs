using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Helpers;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class LoginModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public LoginModel(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public string Login { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public string Error { get; set; } = "";

        public IActionResult OnGet()
        {
            if (CurrentUserId.HasValue)
            {
                if (IsAdmin)
                {
                    return RedirectToPage("/AdminDashboard");
                }

                return RedirectToPage("/Index");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            Login = (Login ?? "").Trim();
            Password = Password ?? "";

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

            if (string.IsNullOrWhiteSpace(Password))
            {
                Error = "Введите пароль";
                return Page();
            }

            if (Password.Length > 100)
            {
                Error = "Пароль слишком длинный";
                return Page();
            }

            var loginForSearch = Login.ToLower();

            var user = _db.Users.FirstOrDefault(u =>
                u.Login.ToLower() == loginForSearch);

            if (user == null)
            {
                Error = "Неверный логин или пароль";
                return Page();
            }

            bool isValid = false;
            bool needsRehash = false;

            try
            {
                var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, Password);
                if (verifyResult == PasswordVerificationResult.Success)
                {
                    isValid = true;
                }
                else if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isValid = true;
                    needsRehash = true;
                }
            }
            catch
            {
                isValid = false;
            }

            if (!isValid && user.Password == Password)
            {
                isValid = true;
                needsRehash = true;
            }

            if (!isValid)
            {
                Error = "Неверный логин или пароль";
                return Page();
            }

            if (!user.IsActive)
            {
                Error = "Пользователь отключен";
                return Page();
            }

            if (needsRehash)
            {
                user.Password = _passwordHasher.HashPassword(user, Password);
                _db.SaveChanges();
            }

            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.Login, user.Login);
            HttpContext.Session.SetString(SessionKeys.Name, user.Name);
            HttpContext.Session.SetString(SessionKeys.Role, user.Role);
            HttpContext.Session.SetString(SessionKeys.Theme, string.IsNullOrWhiteSpace(user.Theme) ? "light" : user.Theme);

            if (user.Role == "admin")
            {
                return RedirectToPage("/AdminDashboard");
            }

            return RedirectToPage("/Index");
        }
    }
}
