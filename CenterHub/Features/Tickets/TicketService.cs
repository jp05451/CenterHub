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

    private const int PmStartHour = 13;

    // Before 13:00 is the morning shift (including anything earlier than the 8:00 start), from 13:00 on is
    // the afternoon shift (including anything after 17:00). Every time maps to exactly one shift.
    public static ShiftPeriod GetShiftPeriod(TimeOnly contactTime) =>
        contactTime.Hour < PmStartHour ? ShiftPeriod.AM : ShiftPeriod.PM;

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
            ShiftPeriod = GetShiftPeriod(model.ContactTime),
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
        await _db.Entry(ticket).ReloadAsync();

        if (!isAdmin && ticket.CreatedByUserId != requestingUserId)
        {
            throw new InvalidOperationException("Only the ticket's creator or an admin may edit it.");
        }

        if (ticket.IsVoided)
        {
            throw new InvalidOperationException("A voided ticket cannot be edited; an admin must restore it first.");
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
        await _db.Entry(ticket).ReloadAsync();

        if (!isAdmin && ticket.CreatedByUserId != requestingUserId)
        {
            throw new InvalidOperationException("Only the ticket's creator or an admin may void it.");
        }

        if (ticket.IsVoided)
        {
            throw new InvalidOperationException("Ticket is already voided.");
        }

        ticket.IsVoided = true;
        ticket.VoidedByUserId = requestingUserId;
        ticket.VoidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RestoreTicketAsync(int ticketId, bool isAdmin)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");
        await _db.Entry(ticket).ReloadAsync();

        if (!isAdmin)
        {
            throw new InvalidOperationException("Only an admin may restore a voided ticket.");
        }

        if (!ticket.IsVoided)
        {
            throw new InvalidOperationException("Ticket is not voided.");
        }

        ticket.IsVoided = false;
        ticket.VoidedByUserId = null;
        ticket.VoidedAt = null;

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
        // Read-only listing: untracked so another session's void/restore shows up on the next search.
        var query = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Categories)
            .AsQueryable();

        if (!filter.IncludeVoided)
            query = query.Where(t => !t.IsVoided);

        if (filter.DateFrom is { } dateFrom)
            query = query.Where(t => t.ShiftDate >= dateFrom);

        if (filter.DateTo is { } dateTo)
            query = query.Where(t => t.ShiftDate <= dateTo);

        if (!string.IsNullOrWhiteSpace(filter.RequestingUnit))
        {
            var unit = filter.RequestingUnit.Trim();
            query = query.Where(t => t.RequestingUnit.Contains(unit));
        }

        if (!string.IsNullOrWhiteSpace(filter.RequesterName))
        {
            var name = filter.RequesterName.Trim();
            query = query.Where(t => t.RequesterName.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(filter.RequesterContact))
        {
            var contact = filter.RequesterContact.Trim();
            query = query.Where(t => t.RequesterContact != null && t.RequesterContact.Contains(contact));
        }

        if (!string.IsNullOrWhiteSpace(filter.CreatedByUserId))
            query = query.Where(t => t.CreatedByUserId == filter.CreatedByUserId);

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
