using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Los ingresos deben ser mayores a 0")]
    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; } = true;
}