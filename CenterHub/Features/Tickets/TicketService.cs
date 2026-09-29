// CenterHub/Features/Tickets/TicketService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.Tickets;

public class TicketService
{
    private readonly AppDbContext _db;

    public TicketService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Ticket> CreateTicketAsync(TicketFormModel model, string createdByUserId)
    {
        var maxSeq = await _db.Tickets
            .Where(t => t.ShiftDate == model.ShiftDate)
            .Select(t => (int?)t.DailySeq)
            .MaxAsync() ?? 0;

        var ticket = new Ticket
        {
            DailySeq = maxSeq + 1,
            ShiftDate = model.ShiftDate,
            ShiftPeriod = model.ShiftPeriod,
            ContactTime = model.ContactTime,
            RequestingUnit = model.RequestingUnit,
            RequesterName = model.RequesterName,
            RequesterContact = model.RequesterContact,
            ServiceMode = model.ServiceMode,
            ResolutionStatus = model.ResolutionStatus,
            SatisfactionRating = model.SatisfactionRating,
            ServiceDurationMinutes = model.ServiceDurationMinutes,
            OperatingSystem = model.OperatingSystem,
            ProblemDescription = model.ProblemDescription,
            SolutionDescription = model.SolutionDescription,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            Categories = model.SelectedCategoryIds
                .Select(categoryId => new TicketCategory { ServiceCategoryId = categoryId })
                .ToList()
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> UpdateTicketAsync(int ticketId, TicketFormModel model, string requestingUserId, bool isAdmin)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Categories)
            .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

        if (!isAdmin && ticket.CreatedByUserId != requestingUserId)
        {
            throw new InvalidOperationException("Only the ticket's creator or an admin may edit it.");
        }

        // ShiftDate/ShiftPeriod/DailySeq are intentionally left untouched here — they drive
        // the per-day ticket numbering, so "moving" a ticket to a different shift after the
        // fact is out of scope; use void + a new ticket for that instead.
        ticket.RequestingUnit = model.RequestingUnit;
        ticket.RequesterName = model.RequesterName;
        ticket.RequesterContact = model.RequesterContact;
        ticket.ServiceMode = model.ServiceMode;
        ticket.ResolutionStatus = model.ResolutionStatus;
        ticket.SatisfactionRating = model.SatisfactionRating;
        ticket.ServiceDurationMinutes = model.ServiceDurationMinutes;
        ticket.OperatingSystem = model.OperatingSystem;
        ticket.ProblemDescription = model.ProblemDescription;
        ticket.SolutionDescription = model.SolutionDescription;
        ticket.LastEditedByUserId = requestingUserId;
        ticket.LastEditedAt = DateTime.UtcNow;

        _db.TicketCategories.RemoveRange(ticket.Categories);
        ticket.Categories = model.SelectedCategoryIds
            .Select(categoryId => new TicketCategory { TicketId = ticketId, ServiceCategoryId = categoryId })
            .ToList();

        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task VoidTicketAsync(int ticketId, string requestingUserId, bool isAdmin)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

        if (!isAdmin && ticket.CreatedByUserId != requestingUserId)
        {
            throw new InvalidOperationException("Only the ticket's creator or an admin may void it.");
        }

        ticket.IsVoided = true;
        ticket.VoidedByUserId = requestingUserId;
        ticket.VoidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
    {
        return await _db.Tickets
            .Include(t => t.Categories)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    public async Task<List<Ticket>> GetTicketsAsync(TicketFilter filter)
    {
        var query = _db.Tickets
            .Include(t => t.Categories)
            .AsQueryable();

        if (!filter.IncludeVoided)
            query = query.Where(t => !t.IsVoided);

        if (filter.ShiftDate is { } shiftDate)
            query = query.Where(t => t.ShiftDate == shiftDate);

        if (!string.IsNullOrWhiteSpace(filter.RequestingUnit))
            query = query.Where(t => t.RequestingUnit.Contains(filter.RequestingUnit));

        if (filter.ServiceCategoryId is { } categoryId)
            query = query.Where(t => t.Categories.Any(c => c.ServiceCategoryId == categoryId));

        if (filter.ResolutionStatus is { } status)
            query = query.Where(t => t.ResolutionStatus == status);

        return await query
            .OrderByDescending(t => t.ShiftDate)
            .ThenByDescending(t => t.DailySeq)
            .ToListAsync();
    }

    public async Task<List<ServiceCategory>> GetActiveCategoriesAsync()
    {
        return await _db.ServiceCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<List<string>> GetKnownRequestingUnitsAsync()
    {
        return await _db.Tickets
            .Select(t => t.RequestingUnit)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();
    }
}
