using Microsoft.AspNetCore.Mvc;
using razorpm.Data;
using razorpm.Services;
using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace razorpm.Pages
{
    public class ForgotPasswordModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;

        public ForgotPasswordModel(AppDbContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [BindProperty]
        public string Email { get; set; } = "";

        public string Error { get; set; } = "";
        public string Message { get; set; } = "";

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
            Email = (Email ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(Email))
            {
                Error = "Введите email";
                return Page();
            }

            if (Email.Length > 100 || !IsValidEmail(Email))
            {
                Error = "Введите корректный email";
                return Page();
            }

            var user = _db.Users.FirstOrDefault(u =>
                u.Email.ToLower() == Email &&
                u.IsActive);

            if (user == null)
            {
                Message = "Письмо отправленно на указанную почту";
                return Page();
            }

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            user.ResetPasswordToken = token;
            user.ResetPasswordTokenExpiresAt = DateTime.Now.AddHours(1);

            _db.SaveChanges();

            var resetLink = Url.Page(
                "/ResetPassword",
                pageHandler: null,
                values: new { token },
                protocol: Request.Scheme);

            var subject = "Восстановление пароля";

            var body =
$@"Здравствуйте, {user.Name}!

Для восстановления пароля перейдите по ссылке:

{resetLink}

Ссылка действует 1 час

Если вы не запрашивали восстановление пароля, просто проигнорируйте это письмо";

            try
            {
                await _emailService.SendAsync(user.Email, subject, body);
            }
            catch
            {
                Error = "Не удалось отправить письмо. Проверьте настройки почты";
                return Page();
            }

            Message = "Письмо отправленно на указанную почту";

            return Page();
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