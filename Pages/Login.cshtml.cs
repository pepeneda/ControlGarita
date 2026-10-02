using System.Security.Claims;
using ControlGarita.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ControlGarita.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly GaritaSecurityService _securityService;

    public LoginModel(IConfiguration config, GaritaSecurityService securityService)
    {
        _config = config;
        _securityService = securityService;
    }

    [BindProperty]
    public string ModoAcceso { get; set; } = "Garita";

    [BindProperty]
    public string? PinGarita { get; set; }

    [BindProperty]
    public string? CodigoVinculacion { get; set; }

    [BindProperty]
    public string? NombreEquipo { get; set; }

    [BindProperty]
    public string? UsuarioAdmin { get; set; }

    [BindProperty]
    public string? PasswordAdmin { get; set; }

    public bool EquipoVinculado { get; set; } = false;
    public string? ErrorMensaje { get; set; }
    public string? ExitoMensaje { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        var token = Request.Cookies["GaritaDeviceToken"];
        EquipoVinculado = await _securityService.EsPuestoValidoAsync(token);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        var tokenDispositivo = Request.Cookies["GaritaDeviceToken"];
        EquipoVinculado = await _securityService.EsPuestoValidoAsync(tokenDispositivo);

        // 1. Acceso puesto de Garita
        if (ModoAcceso == "Garita")
        {
            if (!EquipoVinculado)
            {
                ErrorMensaje = "Este equipo no está autorizado para operar la garita. Solicita un código de vinculación al Supervisor.";
                return Page();
            }

            var pinValido = await _securityService.ObtenerPinGaritaAsync();

            if (PinGarita == pinValido)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, "Puesto Garita"),
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

    // Handler para vincular el equipo usando el código generado por el supervisor
    public async Task<IActionResult> OnPostVincularAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoVinculacion))
        {
            ErrorMensaje = "Debes introducir el código de 6 dígitos facilitado por el supervisor.";
            return Page();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers["User-Agent"].ToString();

        var resultado = await _securityService.CanjearCodigoAsync(
            CodigoVinculacion.Trim(), ip, ua, NombreEquipo ?? "Puesto Garita");

        if (resultado.Exito)
        {
            // Guardar cookie permanente (1 año) en el navegador del equipo
            Response.Cookies.Append("GaritaDeviceToken", resultado.TokenGenerado!, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

            ExitoMensaje = "Equipo vinculado correctamente como puesto de guardia autorizado. Ya puedes introducir el PIN.";
            EquipoVinculado = true;
            return Page();
        }

        ErrorMensaje = resultado.Mensaje;
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}