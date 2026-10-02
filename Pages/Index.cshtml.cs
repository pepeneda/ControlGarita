using ControlGarita.Data;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ControlGarita.Pages;

[Authorize(Policy = "SoloGarita")]
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

        var ahora = DateTime.Now;

        // Previstas: autorizadas activas que NO están dentro del recinto
        PrevistasHoy = await _context.Visitas
            .Where(v => v.Estado == EstadoAcceso.Previsto
                     && v.FechaInicio <= ahora
                     && v.FechaFin >= ahora)
            .OrderBy(v => v.VisitanteEmpresa)
            .ToListAsync();

        // En recinto: los que actualmente están dentro (independientemente de la fecha inicial)
        EnRecinto = await _context.Visitas
            .Where(v => v.Estado == EstadoAcceso.EnRecinto)
            .OrderBy(v => v.VisitanteEmpresa)
            .ToListAsync();
    }
}