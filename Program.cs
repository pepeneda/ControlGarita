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
builder.Services.AddScoped<ControlGarita.Services.GaritaSecurityService>();

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

// Endpoint: Cambiar estado Visita (Entrada / Salida con soporte multiría)
app.MapPost("/api/visitas/cambiar-estado", async (
    [FromBody] CambiarEstadoRequest req, 
    AppDbContext db, 
    IHubContext<AccesosHub> hub) =>
{
    var visita = await db.Visitas.FindAsync(req.VisitaId);
    if (visita == null) return Results.NotFound();

    var ahora = DateTime.Now;
    string tipoMovimiento;

    if (req.Accion == "ENTRADA")
    {
        visita.Estado = EstadoAcceso.EnRecinto;
        tipoMovimiento = "ENTRADA";
    }
    else // "SALIDA"
    {
        tipoMovimiento = "SALIDA";

        // Si la autorización sigue en vigor, vuelve a "Previsto" para siguientes días/accesos
        if (visita.FechaFin >= ahora)
        {
            visita.Estado = EstadoAcceso.Previsto;
        }
        else
        {
            visita.Estado = EstadoAcceso.Completada; // Ya venció el rango
        }
    }

    var log = new RegistroLog
    {
        VisitaId = visita.Id,
        TipoMovimiento = tipoMovimiento,
        FechaHoraExacta = ahora,
        Operador = string.IsNullOrWhiteSpace(req.Operador) ? "Garita" : req.Operador
    };
    db.Logs.Add(log);
    await db.SaveChangesAsync();

    // Notificar a clientes conectados vía SignalR
    await hub.Clients.All.SendAsync("VisitaActualizada", new
    {
        visitaId = visita.Id,
        nuevoEstado = (int)visita.Estado,
        tipoMovimiento = log.TipoMovimiento,
        sigueVigente = visita.FechaFin >= ahora,
        fechaFinStr = visita.FechaFin.ToString("dd/MM/yyyy HH:mm")
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

public record CambiarEstadoRequest(int VisitaId, string Accion, string? Operador);