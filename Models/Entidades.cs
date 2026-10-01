using System.ComponentModel.DataAnnotations;

namespace ControlGarita.Models;

public enum EstadoAcceso
{
    Previsto = 0,
    EnRecinto = 1,
    Salida = 2
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

    public DateTime FechaHoraPrevista { get; set; } = DateTime.Now;

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
    
    // Caducidad automática: por defecto al final del día
    public DateTime FechaExpiracion { get; set; } = DateTime.Today.AddDays(1).AddTicks(-1);

    public bool Activo { get; set; } = true;
    public string Prioridad { get; set; } = "Normal"; // Normal, Urgente
}