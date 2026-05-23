using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GuvenlikKontrolWeb.Pages.Account
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnPostAsync()
        {
            // Tarayýcýdaki oturum bilgilerini tamamen siler
            await HttpContext.SignOutAsync("MyCookieAuth");

            // Çýkýþ yaptýktan sonra Login sayfasýna yönlendirir
            return RedirectToPage("/Account/Login");
        }

        // Eðer birisi direkt URL'den /Account/Logout gitmeye çalýþýrsa da çýkýþ yaptýr
        public async Task<IActionResult> OnGetAsync()
        {
            await HttpContext.SignOutAsync("MyCookieAuth");
            return RedirectToPage("/Account/Login");
        }
    }
}