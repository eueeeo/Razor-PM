using Microsoft.AspNetCore.Mvc;

namespace razorpm.Pages
{
    public class PrivacyModel : BasePageModel
    {
        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            return Page();
        }
    }
}