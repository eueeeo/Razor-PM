using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using razorpm.Data;
using razorpm.Helpers;

namespace razorpm.Pages
{
    public class ThemeModel : PageModel
    {
        private readonly AppDbContext _db;

        public ThemeModel(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult OnPost()
        {
            var userIdText = HttpContext.Session.GetString(SessionKeys.UserId);
            if (!int.TryParse(userIdText, out int userId))
            {
                return RedirectToPage("/Login");
            }

            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            var currentTheme = HttpContext.Session.GetString(SessionKeys.Theme) ?? "light";
            var newTheme = currentTheme == "dark" ? "light" : "dark";

            user.Theme = newTheme;
            _db.SaveChanges();

            HttpContext.Session.SetString(SessionKeys.Theme, newTheme);

            var backUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(backUrl))
            {
                return Redirect(backUrl);
            }

            return RedirectToPage("/Index");
        }
    }
}