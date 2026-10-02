using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ControlGarita.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;

    public LoginModel(IConfiguration config)
    {
        _config = config;
    }

    [BindProperty]
    public string ModoAcceso { get; set; } = "Garita"; // "Garita" o "Supervisor"

    [BindProperty]
    public string? PinGarita { get; set; }

    [BindProperty]
    public string? UsuarioAdmin { get; set; }

    [BindProperty]
    public string? PasswordAdmin { get; set; }

    public string? ErrorMensaje { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        // 1. Acceso puesto de Garita
        if (ModoAcceso == "Garita")
        {
            var pinValido = _config["SecurityConfig:PinGarita"] ?? "1234";

            if (PinGarita == pinValido)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, "Puesto Garita Principal"),
                    new(ClaimTypes.Role, "Garita")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true });

                return LocalRedirect(returnUrl);
            }

            ErrorMensaje = "PIN de Garita incorrecto.";
            return Page();
        }

        // 2. Acceso local de Contingencia para Supervisor
        if (ModoAcceso == "Supervisor")
        {
            var userValido = _config["SecurityConfig:AdminEmergencia:Usuario"] ?? "adminlocal";
            var passValido = _config["SecurityConfig:AdminEmergencia:Password"] ?? "ClaveSeguraLocal2026!";

            if (UsuarioAdmin == userValido && PasswordAdmin == passValido)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, UsuarioAdmin),
                    new(ClaimTypes.Role, "Supervisor")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = false });

                return LocalRedirect(Url.Page("/Supervisor") ?? returnUrl);
            }

            ErrorMensaje = "Usuario o contraseña de contingencia incorrectos.";
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}