using ControlGarita.Data;
using ControlGarita.Hubs;
using ControlGarita.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configuración de SQL Server desde appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost\\SQLEXPRESS;Database=ControlAccesosDB;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

var app = builder.Build();

// Asegurar que la base de datos SQL Server y sus tablas existen al iniciar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<AccesosHub>("/accesosHub");

// Endpoint: Cambiar estado Visita (Entrada / Salida)
app.MapPost("/api/visitas/cambiar-estado", async (
    [FromBody] CambiarEstadoRequest req, 
    AppDbContext db, 
    IHubContext<AccesosHub> hub) =>
{
    var visita = await db.Visitas.FindAsync(req.VisitaId);
    if (visita == null) return Results.NotFound();

    visita.Estado = req.NuevoEstado;

    var log = new RegistroLog
    {
        VisitaId = visita.Id,
        TipoMovimiento = req.NuevoEstado == EstadoAcceso.EnRecinto ? "ENTRADA" : "SALIDA",
        FechaHoraExacta = DateTime.Now,
        Operador = string.IsNullOrWhiteSpace(req.Operador) ? "Garita" : req.Operador
    };
    db.Logs.Add(log);
    await db.SaveChangesAsync();

    await hub.Clients.All.SendAsync("VisitaActualizada", new
    {
        visitaId = visita.Id,
        nuevoEstado = (int)visita.Estado,
        horaLog = log.FechaHoraExacta.ToString("HH:mm:ss"),
        tipoMovimiento = log.TipoMovimiento
    });

    return Results.Ok();
});

// Endpoint: Retirar / Dar de baja un aviso en tiempo real
app.MapPost("/api/comunicados/retirar/{id:int}", async (
    int id, 
    AppDbContext db, 
    IHubContext<AccesosHub> hub) =>
{
    var comunicado = await db.Comunicados.FindAsync(id);
    if (comunicado == null) return Results.NotFound();

    comunicado.Activo = false;
    await db.SaveChangesAsync();

    // Notificar a la garita para que el aviso desaparezca al instante
    await hub.Clients.All.SendAsync("AvisoRetirado", id);

    return Results.Ok();
});

app.Run();

public record CambiarEstadoRequest(int VisitaId, EstadoAcceso NuevoEstado, string? Operador);