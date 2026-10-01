using ControlGarita.Data;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ControlGarita.Pages;

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