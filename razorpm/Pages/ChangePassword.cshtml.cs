using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Models;
using System.Linq;

namespace razorpm.Pages
{
    public class ChangePasswordModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public ChangePasswordModel(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public string CurrentPassword { get; set; } = "";

        [BindProperty]
        public string NewPassword { get; set; } = "";

        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        public string Error { get; set; } = "";
        public string Success { get; set; } = "";

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

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
            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                Error = "Введите текущий пароль";
                return Page();
            }

            var passwordError = GetPasswordValidationError(NewPassword);
            if (!string.IsNullOrWhiteSpace(passwordError))
            {
                Error = passwordError;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                Error = "Подтвердите новый пароль";
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                Error = "Новый пароль и подтверждение не совпадают";
                return Page();
            }

            if (NewPassword == CurrentPassword)
            {
                Error = "Новый пароль должен отличаться от текущего";
                return Page();
            }

            bool isCurrentValid = false;
            try
            {
                var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, CurrentPassword);
                if (verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isCurrentValid = true;
                }
            }
            catch
            {
                isCurrentValid = false;
            }

            if (!isCurrentValid && user.Password == CurrentPassword)
            {
                isCurrentValid = true;
            }

            if (!isCurrentValid)
            {
                Error = "Текущий пароль введен неверно";
                return Page();
            }

            user.Password = _passwordHasher.HashPassword(user, NewPassword);
            _db.SaveChanges();

            CurrentPassword = "";
            NewPassword = "";
            ConfirmPassword = "";
            Success = "Пароль успешно изменен";

            return Page();
        }

        private static string GetPasswordValidationError(string? password)
        {
            password ??= "";

            if (string.IsNullOrWhiteSpace(password))
            {
                return "Введите новый пароль";
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
