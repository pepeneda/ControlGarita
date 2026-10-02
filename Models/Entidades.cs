using System.ComponentModel.DataAnnotations;

namespace ControlGarita.Models;

public enum EstadoAcceso
{
    Previsto = 0,    // Autorizado y pendiente de entrar (o fuera del recinto)
    EnRecinto = 1,   // Dentro de las instalaciones
    Completada = 2   // Autorización expirada o finalizada definitivamente
}

public class Visita
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre o empresa es obligatorio")]
    public string VisitanteEmpresa { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DNI/Documento es obligatorio")]
    public string Dni { get; set; } = string.Empty;

    public string? Matricula { get; set; }

    [Required(ErrorMessage = "Indica el contacto o dependencia de destino")]
    public string ContactoDestino { get; set; } = string.Empty;

    // Rango de fechas de autorización
    public DateTime FechaInicio { get; set; } = DateTime.Today;
    public DateTime FechaFin { get; set; } = DateTime.Today.AddDays(1).AddTicks(-1); // Fin del día por defecto

    public EstadoAcceso Estado { get; set; } = EstadoAcceso.Previsto;

    public string? NumeroPase { get; set; }
    public string? Observaciones { get; set; }

    public List<RegistroLog>? Logs { get; set; } = new();
}

public class RegistroLog
{
    public int Id { get; set; }

    public int VisitaId { get; set; }
    public Visita? Visita { get; set; }

    public string TipoMovimiento { get; set; } = string.Empty; // "ENTRADA" o "SALIDA"
    public DateTime FechaHoraExacta { get; set; } = DateTime.Now;
    public string Operador { get; set; } = "Garita";
}

public class Comunicado
{
    public int Id { get; set; }

    [Required]
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime FechaPublicacion { get; set; } = DateTime.Now;
    public DateTime FechaExpiracion { get; set; } = DateTime.Today.AddDays(1).AddTicks(-1);
    public bool Activo { get; set; } = true;
    public string Prioridad { get; set; } = "Normal";
}

public class ConfiguracionSistema
{
    [Key]
    public string Clave { get; set; } = string.Empty; // Ej: "PinGarita"
    public string Valor { get; set; } = string.Empty;
    public DateTime FechaModificacion { get; set; } = DateTime.Now;
    public string ModificadoPor { get; set; } = "Sistema";
}

public class PuestoAutorizado
{
    public int Id { get; set; }
    public string NombrePuesto { get; set; } = "Garita Principal";
    public string TokenIdentificador { get; set; } = Guid.NewGuid().ToString("N");
    public string? IpVinculacion { get; set; }
    public string? UserAgent { get; set; }
    public DateTime FechaVinculacion { get; set; } = DateTime.Now;
    public DateTime UltimaActividad { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
}

public class CodigoEnlaceTemporal
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // Código corto tipo 6 dígitos: "784920"
    public DateTime FechaExpiracion { get; set; }
    public bool Utilizado { get; set; } = false;
    public string CreadoPor { get; set; } = string.Empty;
}