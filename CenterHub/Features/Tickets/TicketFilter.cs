// CenterHub/Features/Tickets/TicketFilter.cs
using CenterHub.Models;

namespace CenterHub.Features.Tickets;

public class TicketFilter
{
    public DateOnly? ShiftDate { get; set; }
    public string? RequestingUnit { get; set; }
    public int? ServiceCategoryId { get; set; }
    public ResolutionStatus? ResolutionStatus { get; set; }

    // Default false: voided tickets are hidden unless explicitly asked for.
    public bool IncludeVoided { get; set; }
}
