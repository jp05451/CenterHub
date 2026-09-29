namespace CenterHub.Models;

public class Ticket
{
    public int Id { get; set; }

    // Per-day sequence number, computed by TicketService — see Task 4.
    public int DailySeq { get; set; }

    public DateOnly ShiftDate { get; set; }
    public ShiftPeriod ShiftPeriod { get; set; }
    public TimeOnly ContactTime { get; set; }

    public string RequestingUnit { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterContact { get; set; }

    public ServiceMode ServiceMode { get; set; }
    public ResolutionStatus ResolutionStatus { get; set; }
    public int? SatisfactionRating { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public string? OperatingSystem { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;
    public string? SolutionDescription { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set by TicketService.UpdateTicketAsync; null means never edited since creation.
    public string? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    // Soft delete: voided tickets stay in the database (for audit/history) but are
    // excluded from the default ticket list — a hard delete would silently change
    // numbering and history that was already reported. See TicketService.VoidTicketAsync.
    public bool IsVoided { get; set; }
    public string? VoidedByUserId { get; set; }
    public DateTime? VoidedAt { get; set; }

    public List<TicketCategory> Categories { get; set; } = new();
}
