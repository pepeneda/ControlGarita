using ControlGarita.Data;
using ControlGarita.Hubs;
using ControlGarita.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configuración de SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=ControlAccesosDB;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 1. Configurar Autenticación Híbrida (Cookies + Windows Negotiate)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Puesto desatendido
})
.AddNegotiate(); // Windows Authentication / Kerberos

// 2. Configurar Políticas de Autorización
var grupoLdap = builder.Configuration["SecurityConfig:GrupoLdapSupervisores"] ?? @"DOMINIO\G_Supervisores_Accesos";

builder.Services.AddAuthorization(options =>
{
    // Política para el puesto de guardia
    options.AddPolicy("SoloGarita", policy =>
    {
        policy.RequireRole("Garita", "Supervisor");
    });

    // Política para el panel de supervisor (LDAP o Contingencia Local)
    options.AddPolicy("SoloSupervisores", policy =>
    {
        policy.RequireAssertion(context =>
        {
            // Vía 1: Autenticación integrada de Windows con pertenencia al grupo AD
            if (context.User.Identity?.AuthenticationType == NegotiateDefaults.AuthenticationScheme)
            {
                return context.User.IsInRole(grupoLdap);
            }

            // Vía 2: Sesión de cookie local con rol Supervisor (Contingencia)
            return context.User.IsInRole("Supervisor");
        });
    });
});

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

var app = builder.Build();

// Asegurar base de datos
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

// Pipeline de seguridad (en este orden exacto)
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<AccesosHub>("/accesosHub");

// Endpoint: Cambiar estado Visita
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

// Endpoint: Retirar comunicado
app.MapPost("/api/comunicados/retirar/{id:int}", async (
    int id, 
    AppDbContext db, 
    IHubContext<AccesosHub> hub) =>
{
    var comunicado = await db.Comunicados.FindAsync(id);
    if (comunicado == null) return Results.NotFound();

    comunicado.Activo = false;
    await db.SaveChangesAsync();

    await hub.Clients.All.SendAsync("AvisoRetirado", id);

    return Results.Ok();
});

app.Run();

public record CambiarEstadoRequest(int VisitaId, EstadoAcceso NuevoEstado, string? Operador);