using ControlGarita.Data;
using ControlGarita.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGarita.Services;

public class GaritaSecurityService
{
    private readonly AppDbContext _db;

    public GaritaSecurityService(AppDbContext db)
    {
        _db = db;
    }

    // Obtener PIN actual (si no existe en BD, inicializa con 1234)
    public async Task<string> ObtenerPinGaritaAsync()
    {
        var config = await _db.Configuraciones.FindAsync("PinGarita");
        if (config == null)
        {
            config = new ConfiguracionSistema
            {
                Clave = "PinGarita",
                Valor = "1234",
                ModificadoPor = "Sistema"
            };
            _db.Configuraciones.Add(config);
            await _db.SaveChangesAsync();
        }
        return config.Valor;
    }

    // Actualizar PIN
    public async Task ActualizarPinGaritaAsync(string nuevoPin, string usuarioModificador)
    {
        var config = await _db.Configuraciones.FindAsync("PinGarita");
        if (config == null)
        {
            config = new ConfiguracionSistema { Clave = "PinGarita" };
            _db.Configuraciones.Add(config);
        }
        config.Valor = nuevoPin;
        config.FechaModificacion = DateTime.Now;
        config.ModificadoPor = usuarioModificador;
        await _db.SaveChangesAsync();
    }

    // Verificar si el token del navegador corresponde a un puesto activo
    public async Task<bool> EsPuestoValidoAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var puesto = await _db.PuestosAutorizados
            .FirstOrDefaultAsync(p => p.TokenIdentificador == token && p.Activo);

        if (puesto != null)
        {
            puesto.UltimaActividad = DateTime.Now;
            await _db.SaveChangesAsync();
            return true;
        }

        return false;
    }

    // Generar código de 6 dígitos para autorizar un PC
    public async Task<string> GenerarCodigoEnlaceAsync(string usuario)
    {
        // Invalidar códigos previos no usados
        var viejos = await _db.CodigosEnlace
            .Where(c => !c.Utilizado && c.FechaExpiracion > DateTime.Now)
            .ToListAsync();
        _db.CodigosEnlace.RemoveRange(viejos);

        var random = new Random();
        var codigo = random.Next(100000, 999999).ToString();

        _db.CodigosEnlace.Add(new CodigoEnlaceTemporal
        {
            Codigo = codigo,
            FechaExpiracion = DateTime.Now.AddMinutes(15),
            CreadoPor = usuario
        });

        await _db.SaveChangesAsync();
        return codigo;
    }

    // Canjear código desde el PC de garita
    public async Task<(bool Exito, string? TokenGenerado, string Mensaje)> CanjearCodigoAsync(
        string codigo, string? ip, string? userAgent, string nombrePuesto)
    {
        var item = await _db.CodigosEnlace
            .FirstOrDefaultAsync(c => c.Codigo == codigo && !c.Utilizado && c.FechaExpiracion >= DateTime.Now);

        if (item == null)
        {
            return (false, null, "Código de vinculación inválido o caducado.");
        }

        item.Utilizado = true;

        var nuevoPuesto = new PuestoAutorizado
        {
            NombrePuesto = string.IsNullOrWhiteSpace(nombrePuesto) ? "Puesto Garita" : nombrePuesto,
            TokenIdentificador = Guid.NewGuid().ToString("N"),
            IpVinculacion = ip ?? "Desconocida",
            UserAgent = userAgent,
            FechaVinculacion = DateTime.Now,
            UltimaActividad = DateTime.Now,
            Activo = true
        };

        _db.PuestosAutorizados.Add(nuevoPuesto);
        await _db.SaveChangesAsync();

        return (true, nuevoPuesto.TokenIdentificador, "Equipo vinculado correctamente.");
    }
}