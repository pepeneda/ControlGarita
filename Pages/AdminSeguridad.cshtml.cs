using ControlGarita.Data;
using ControlGarita.Models;
using ControlGarita.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ControlGarita.Pages;

[Authorize(Policy = "SoloSupervisores")]
public class AdminSeguridadModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GaritaSecurityService _securityService;

    public AdminSeguridadModel(AppDbContext db, GaritaSecurityService securityService)
    {
        _db = db;
        _securityService = securityService;
    }

    [BindProperty]
    public string NuevoPin { get; set; } = string.Empty;

    public string PinActual { get; set; } = string.Empty;
    public string? CodigoGenerado { get; set; }
    public List<PuestoAutorizado> Puestos { get; set; } = new();

    public string? MensajeExito { get; set; }

    public async Task OnGetAsync()
    {
        PinActual = await _securityService.ObtenerPinGaritaAsync();
        Puestos = await _db.PuestosAutorizados.OrderByDescending(p => p.FechaVinculacion).ToListAsync();
    }

    // Cambiar PIN de la garita
    public async Task<IActionResult> OnPostCambiarPinAsync()
    {
        if (!string.IsNullOrWhiteSpace(NuevoPin) && NuevoPin.Length >= 4)
        {
            var usuario = User.Identity?.Name ?? "Supervisor";
            await _securityService.ActualizarPinGaritaAsync(NuevoPin.Trim(), usuario);
            MensajeExito = "El PIN de Garita se ha actualizado correctamente a: " + NuevoPin;
        }

        return await RefrescarAsync();
    }

    // Generar código de vinculación para autorizar un PC
    public async Task<IActionResult> OnPostGenerarCodigoAsync()
    {
        var usuario = User.Identity?.Name ?? "Supervisor";
        CodigoGenerado = await _securityService.GenerarCodigoEnlaceAsync(usuario);
        MensajeExito = "Código generado con éxito. Válido durante 15 minutos.";

        return await RefrescarAsync();
    }

    // Revocar un equipo autorizado
    public async Task<IActionResult> OnPostRevocarPuestoAsync(int id)
    {
        var puesto = await _db.PuestosAutorizados.FindAsync(id);
        if (puesto != null)
        {
            puesto.Activo = false;
            await _db.SaveChangesAsync();
            MensajeExito = $"El puesto '{puesto.NombrePuesto}' ha sido revocado. Ya no podrá iniciar sesión.";
        }

        return await RefrescarAsync();
    }

    private async Task<IActionResult> RefrescarAsync()
    {
        PinActual = await _securityService.ObtenerPinGaritaAsync();
        Puestos = await _db.PuestosAutorizados.OrderByDescending(p => p.FechaVinculacion).ToListAsync();
        return Page();
    }
}