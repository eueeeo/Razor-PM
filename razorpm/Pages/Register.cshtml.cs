using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Helpers;
using razorpm.Models;
using razorpm.Services;
using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace razorpm.Pages
{
    public class RegisterModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;
        private readonly IPasswordHasher<User> _passwordHasher;

        public RegisterModel(AppDbContext db, EmailService emailService, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public string Name { get; set; } = "";

        [BindProperty]
        public string Login { get; set; } = "";

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        public string Error { get; set; } = "";

        public IActionResult OnGet()
        {
            if (CurrentUserId.HasValue)
            {
                return RedirectToPage("/Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Name = (Name ?? "").Trim();
            Login = (Login ?? "").Trim();
            Email = (Email ?? "").Trim().ToLower();
            Password = Password ?? "";
            ConfirmPassword = ConfirmPassword ?? "";

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

            if (string.IsNullOrWhiteSpace(Email))
            {
                Error = "Введите email";
                return Page();
            }

            if (Email.Length > 100)
            {
                Error = "Email не должен быть длиннее 100 символов";
                return Page();
            }

            if (!IsValidEmail(Email))
            {
                Error = "Введите корректный email";
                return Page();
            }

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

            var loginForSearch = Login.ToLower();

            var loginExists = _db.Users.Any(u => u.Login.ToLower() == loginForSearch);
            if (loginExists)
            {
                Error = "Такой логин уже существует";
                return Page();
            }

            var emailExists = _db.Users.Any(u => u.Email.ToLower() == Email);
            if (emailExists)
            {
                Error = "Такой email уже существует";
                return Page();
            }

            var user = new User
            {
                Name = Name,
                Login = Login,
                Email = Email,
                Password = "",
                Role = "user",
                Theme = "light",
                IsActive = true
            };

            user.Password = _passwordHasher.HashPassword(user, Password);

            _db.Users.Add(user);
            _db.SaveChanges();

            try
            {
                await SendRegistrationEmailAsync(user);
            }
            catch
            {
            }

            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.Login, user.Login);
            HttpContext.Session.SetString(SessionKeys.Name, user.Name);
            HttpContext.Session.SetString(SessionKeys.Role, user.Role);
            HttpContext.Session.SetString(SessionKeys.Theme, user.Theme);

            return RedirectToPage("/Index");
        }

        private async Task SendRegistrationEmailAsync(User user)
        {
            var subject = "Регистрация в RazorPM";

            var body =
$@"Здравствуйте, {user.Name}!

Вы успешно зарегистрировались в системе RazorPM.

Ваш логин: {user.Login}
Роль: пользователь

Теперь вы можете создавать проекты, работать с задачами, отслеживать сроки и пользоваться календарём.

Это автоматическое письмо, отвечать на него не нужно.";

            await _emailService.SendAsync(user.Email, subject, body);
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

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
