using Microsoft.AspNetCore.Mvc;

namespace razorpm.Pages
{
    public class LogoutModel : BasePageModel
    {
        public IActionResult OnGet()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }

        public IActionResult OnPost()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}