using Microsoft.EntityFrameworkCore;
using ControlGarita.Models;

namespace ControlGarita.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Visita> Visitas => Set<Visita>();
    public DbSet<RegistroLog> Logs => Set<RegistroLog>();
    public DbSet<Comunicado> Comunicados => Set<Comunicado>();
}