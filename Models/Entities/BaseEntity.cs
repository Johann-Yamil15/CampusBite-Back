namespace CampusBite_Back.Models.Entities;

// JJ-Sprint1 02/10/2026: entidad base con llave primaria y campos de auditoría
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
