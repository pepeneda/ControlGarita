using ControlGarita.Data;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ControlGarita.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Visita> PrevistasHoy { get; set; } = new();
    public List<Visita> EnRecinto { get; set; } = new();
    public List<Comunicado> ComunicadosActivos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var limiteHoy = DateTime.Today.AddDays(1);

        // Cargar avisos activos ordenando primero los urgentes
       ComunicadosActivos = await _context.Comunicados
            .Where(c => c.Activo && c.FechaExpiracion > DateTime.Now)
            .OrderByDescending(c => c.Prioridad == "Urgente")
            .ThenByDescending(c => c.FechaPublicacion)
            .ToListAsync();

        // Cargar visitas en estado 'Previsto' hasta el final del día de hoy
        PrevistasHoy = await _context.Visitas
            .Where(v => v.Estado == EstadoAcceso.Previsto && v.FechaHoraPrevista < limiteHoy)
            .OrderBy(v => v.FechaHoraPrevista)
            .ToListAsync();

        // Cargar visitas que ya están dentro del recinto
        EnRecinto = await _context.Visitas
            .Where(v => v.Estado == EstadoAcceso.EnRecinto)
            .OrderByDescending(v => v.Id)
            .ToListAsync();
    }
}