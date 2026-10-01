using ControlGarita.Data;
using ControlGarita.Hubs;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ControlGarita.Pages;

public class SupervisorModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IHubContext<AccesosHub> _hub;

    public SupervisorModel(AppDbContext context, IHubContext<AccesosHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    [BindProperty]
    public Visita NuevaVisita { get; set; } = new();

    [BindProperty]
    public Comunicado NuevoComunicado { get; set; } = new();

    public List<Comunicado> AvisosVigentes { get; set; } = new();

    [TempData]
    public string? MensajeExito { get; set; }

    public async Task OnGetAsync()
    {
        var ahora = DateTime.Now;
        NuevaVisita.FechaHoraPrevista = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0);
        NuevoComunicado.FechaExpiracion = DateTime.Today.AddDays(1).AddHours(8); // Por defecto mañana a las 08:00

        // Cargar solo avisos activos no caducados
        AvisosVigentes = await _context.Comunicados
            .Where(c => c.Activo && c.FechaExpiracion > DateTime.Now)
            .OrderByDescending(c => c.FechaPublicacion)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCrearVisitaAsync()
    {
        if (string.IsNullOrWhiteSpace(NuevaVisita.VisitanteEmpresa) || 
            string.IsNullOrWhiteSpace(NuevaVisita.Dni) || 
            string.IsNullOrWhiteSpace(NuevaVisita.ContactoDestino))
        {
            ModelState.AddModelError(string.Empty, "Faltan campos obligatorios.");
            return Page();
        }

        NuevaVisita.Estado = EstadoAcceso.Previsto;
        _context.Visitas.Add(NuevaVisita);
        await _context.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("NuevaVisitaCreada", new
        {
            id = NuevaVisita.Id,
            visitanteEmpresa = NuevaVisita.VisitanteEmpresa,
            dni = NuevaVisita.Dni,
            matricula = NuevaVisita.Matricula ?? "A pie",
            contactoDestino = NuevaVisita.ContactoDestino,
            horaPrevista = NuevaVisita.FechaHoraPrevista.ToString("HH:mm"),
            observaciones = NuevaVisita.Observaciones ?? ""
        });

        MensajeExito = $"Visita para {NuevaVisita.VisitanteEmpresa} enviada a garita.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCrearComunicadoAsync()
    {
        if (string.IsNullOrWhiteSpace(NuevoComunicado.Titulo))
        {
            return Page();
        }

        NuevoComunicado.FechaPublicacion = DateTime.Now;
        NuevoComunicado.Activo = true;
        
        // Si no se asignó fecha de expiración, fin del día
        if (NuevoComunicado.FechaExpiracion <= DateTime.Now)
        {
            NuevoComunicado.FechaExpiracion = DateTime.Today.AddDays(1);
        }

        _context.Comunicados.Add(NuevoComunicado);
        await _context.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("NuevoAvisoPublicado", new
        {
            id = NuevoComunicado.Id,
            titulo = NuevoComunicado.Titulo,
            detalle = NuevoComunicado.Detalle,
            prioridad = NuevoComunicado.Prioridad
        });

        MensajeExito = "Aviso publicado en tiempo real.";
        return RedirectToPage();
    }
}