using ControlGarita.Data;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ControlGarita.Pages;

[Authorize(Policy = "SoloSupervisores")]
public class AuditoriaModel : PageModel
{
    private readonly AppDbContext _context;

    public AuditoriaModel(AppDbContext context)
    {
        _context = context;
    }

    public List<RegistroLog> Registros { get; set; } = new();

    public async Task OnGetAsync()
    {
        Registros = await _context.Logs
            .Include(l => l.Visita)
            .OrderByDescending(l => l.FechaHoraExacta)
            .Take(100)
            .ToListAsync();
    }
}