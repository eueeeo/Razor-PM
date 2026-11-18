using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using razorpm.Data;
using razorpm.Models;
using razorpm.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace razorpm.Pages
{
    public class ContactModel : BasePageModel
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;
        private readonly EmailSettings _emailSettings;

        public ContactModel(
            AppDbContext db,
            EmailService emailService,
            IOptions<EmailSettings> emailSettings)
        {
            _db = db;
            _emailService = emailService;
            _emailSettings = emailSettings.Value;
        }

        [BindProperty]
        public string Subject { get; set; } = "";

        [BindProperty]
        public string MessageText { get; set; } = "";

        public string Error { get; set; } = "";
        public string Success { get; set; } = "";

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            Subject = (Subject ?? "").Trim();
            MessageText = (MessageText ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Subject))
            {
                Error = "Введите тему сообщения";
                return Page();
            }

            if (Subject.Length < 3)
            {
                Error = "Тема должна быть не короче 3 символов";
                return Page();
            }

            if (Subject.Length > 120)
            {
                Error = "Тема не должна быть длиннее 120 символов";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(MessageText))
            {
                Error = "Введите сообщение";
                return Page();
            }

            if (MessageText.Length < 10)
            {
                Error = "Сообщение должно быть не короче 10 символов";
                return Page();
            }

            if (MessageText.Length > 2000)
            {
                Error = "Сообщение не должно быть длиннее 2000 символов";
                return Page();
            }

            if (!CurrentUserId.HasValue)
            {
                return RedirectToPage("/Login");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == CurrentUserId.Value);
            if (user == null || !user.IsActive)
            {
                return RedirectToPage("/Login");
            }

            var toEmail = string.IsNullOrWhiteSpace(_emailSettings.SupportEmail)
                ? _emailSettings.SenderEmail
                : _emailSettings.SupportEmail;

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Error = "Почта администратора не настроена";
                return Page();
            }

            var subject = $"[RazorPM] {Subject}";

            var body =
$@"Сообщение из RazorPM

Пользователь: {user.Name}
Логин: {user.Login}
Email: {user.Email}
Дата: {DateTime.Now:dd.MM.yyyy HH:mm}

Тема: {Subject}

Сообщение:
{MessageText}";

            try
            {
                await _emailService.SendAsync(toEmail, subject, body);
            }
            catch
            {
                Error = "Не удалось отправить сообщение. Проверьте настройки почты.";
                return Page();
            }

            Success = "Сообщение отправлено администратору";
            Subject = "";
            MessageText = "";

            return Page();
        }
    }
}