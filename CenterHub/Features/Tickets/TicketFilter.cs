// CenterHub/Features/Tickets/TicketFilter.cs
using CenterHub.Models;

namespace CenterHub.Features.Tickets;

// All set conditions are ANDed. Text conditions are partial matches; blank means "ignore".
public class TicketFilter
{
    // Inclusive range on ShiftDate; either end may be null (open-ended).
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    public string? RequestingUnit { get; set; }
    public string? RequesterName { get; set; }

    // 分機/學號 share one column (Ticket.RequesterContact).
    public string? RequesterContact { get; set; }

    // 承辦人 = the user who created the ticket.
    public string? CreatedByUserId { get; set; }

    public int? ServiceCategoryId { get; set; }
    public ResolutionStatus? ResolutionStatus { get; set; }

    // Default false: voided tickets are hidden unless explicitly asked for.
    public bool IncludeVoided { get; set; }
}
