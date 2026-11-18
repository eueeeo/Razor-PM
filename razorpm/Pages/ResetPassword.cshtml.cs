using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Linq;

namespace razorpm.Pages
{
    public class ResetPasswordModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public ResetPasswordModel(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        public string Error { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsTokenValid { get; private set; }

        public IActionResult OnGet()
        {
            if (CurrentUserId.HasValue)
            {
                return RedirectToPage("/Index");
            }

            Token = (Token ?? "").Trim();

            IsTokenValid = _db.Users.Any(u =>
                u.ResetPasswordToken == Token &&
                u.ResetPasswordTokenExpiresAt.HasValue &&
                u.ResetPasswordTokenExpiresAt.Value >= DateTime.Now &&
                u.IsActive);

            return Page();
        }

        public IActionResult OnPost()
        {
            Token = (Token ?? "").Trim();
            Password = Password ?? "";
            ConfirmPassword = ConfirmPassword ?? "";

            var user = _db.Users.FirstOrDefault(u =>
                u.ResetPasswordToken == Token &&
                u.ResetPasswordTokenExpiresAt.HasValue &&
                u.ResetPasswordTokenExpiresAt.Value >= DateTime.Now &&
                u.IsActive);

            if (user == null)
            {
                IsTokenValid = false;
                Error = "Ссылка восстановления недействительна или устарела";
                return Page();
            }

            IsTokenValid = true;

            var passwordError = GetPasswordValidationError(Password);
            if (!string.IsNullOrWhiteSpace(passwordError))
            {
                Error = passwordError;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                Error = "Подтвердите пароль";
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                Error = "Пароли не совпадают";
                return Page();
            }

            user.Password = _passwordHasher.HashPassword(user, Password);
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiresAt = null;

            _db.SaveChanges();

            Message = "Пароль успешно изменён. Теперь можно войти.";

            return Page();
        }

        private static string GetPasswordValidationError(string? password)
        {
            password ??= "";

            if (string.IsNullOrWhiteSpace(password))
            {
                return "Введите пароль";
            }

            if (password.Length < 8)
            {
                return "Пароль должен быть не короче 8 символов";
            }

            if (password.Length > 100)
            {
                return "Пароль слишком длинный";
            }

            if (!password.Any(char.IsUpper))
            {
                return "Пароль должен содержать хотя бы одну большую букву";
            }

            if (!password.Any(char.IsLower))
            {
                return "Пароль должен содержать хотя бы одну маленькую букву";
            }

            if (!password.Any(char.IsDigit))
            {
                return "Пароль должен содержать хотя бы одну цифру";
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                return "Пароль должен содержать хотя бы один спецсимвол";
            }

            return "";
        }
    }
}
