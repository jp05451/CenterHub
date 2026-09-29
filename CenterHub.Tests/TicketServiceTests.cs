// CenterHub.Tests/TicketServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.Tickets;
using CenterHub.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CenterHub.Tests;

public class TicketServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public TicketServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static TicketFormModel MakeModel(DateOnly date, ShiftPeriod period) => new()
    {
        ShiftDate = date,
        ShiftPeriod = period,
        ContactTime = new TimeOnly(14, 25),
        RequestingUnit = "材料系",
        RequesterName = "馬同學",
        RequesterContact = "M11404145",
        ServiceMode = ServiceMode.OnSite,
        SelectedCategoryIds = new List<int> { 8 },
        ResolutionStatus = ResolutionStatus.Completed,
        SatisfactionRating = 5,
        ServiceDurationMinutes = 5,
        OperatingSystem = "Windows",
        ProblemDescription = "忘記M365密碼",
        SolutionDescription = "重設M365密碼"
    };

    [Fact]
    public async Task CreateTicketAsync_FirstTicketOfDay_GetsSeqOne()
    {
        var service = new TicketService(_db);
        var date = new DateOnly(2026, 9, 8);

        var ticket = await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.AM), "user-1");

        Assert.Equal(1, ticket.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_SecondTicketSameDay_GetsSeqTwo_EvenAcrossPeriods()
    {
        var service = new TicketService(_db);
        var date = new DateOnly(2026, 9, 8);

        await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.AM), "user-1");
        var second = await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.PM), "user-2");

        Assert.Equal(2, second.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_TicketOnDifferentDay_RestartsAtOne()
    {
        var service = new TicketService(_db);

        await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");
        var nextDay = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 9), ShiftPeriod.AM), "user-1");

        Assert.Equal(1, nextDay.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_PersistsSelectedCategories()
    {
        var service = new TicketService(_db);
        var model = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        model.SelectedCategoryIds = new List<int> { 1, 3 };

        var ticket = await service.CreateTicketAsync(model, "user-1");

        var saved = await _db.Tickets
            .Include(t => t.Categories)
            .FirstAsync(t => t.Id == ticket.Id);
        Assert.Equal(new[] { 1, 3 }, saved.Categories.Select(c => c.ServiceCategoryId).OrderBy(x => x));
    }

    [Fact]
    public async Task GetTicketsAsync_FiltersByShiftDateAndResolutionStatus()
    {
        var service = new TicketService(_db);
        var wanted = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        wanted.ResolutionStatus = ResolutionStatus.Completed;
        await service.CreateTicketAsync(wanted, "user-1");

        var wrongDate = MakeModel(new DateOnly(2026, 9, 9), ShiftPeriod.AM);
        await service.CreateTicketAsync(wrongDate, "user-1");

        var wrongStatus = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.PM);
        wrongStatus.ResolutionStatus = ResolutionStatus.Incomplete;
        await service.CreateTicketAsync(wrongStatus, "user-1");

        var results = await service.GetTicketsAsync(new TicketFilter
        {
            ShiftDate = new DateOnly(2026, 9, 8),
            ResolutionStatus = ResolutionStatus.Completed
        });

        var ticket = Assert.Single(results);
        Assert.Equal(new DateOnly(2026, 9, 8), ticket.ShiftDate);
        Assert.Equal(ResolutionStatus.Completed, ticket.ResolutionStatus);
    }

    [Fact]
    public async Task GetTicketsAsync_FiltersByServiceCategory()
    {
        var service = new TicketService(_db);
        var withCategory1 = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        withCategory1.SelectedCategoryIds = new List<int> { 1 };
        await service.CreateTicketAsync(withCategory1, "user-1");

        var withCategory2 = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        withCategory2.SelectedCategoryIds = new List<int> { 2 };
        await service.CreateTicketAsync(withCategory2, "user-1");

        var results = await service.GetTicketsAsync(new TicketFilter { ServiceCategoryId = 2 });

        Assert.Single(results);
    }

    [Fact]
    public async Task UpdateTicketAsync_ByOwner_UpdatesContentAndStampsLastEditedBy()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        var edit = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        edit.ProblemDescription = "更正後的問題描述";
        edit.SelectedCategoryIds = new List<int> { 2, 3 };

        var updated = await service.UpdateTicketAsync(ticket.Id, edit, "user-1", isAdmin: false);

        Assert.Equal("更正後的問題描述", updated.ProblemDescription);
        Assert.Equal("user-1", updated.LastEditedByUserId);
        Assert.NotNull(updated.LastEditedAt);
        Assert.Equal(new[] { 2, 3 }, updated.Categories.Select(c => c.ServiceCategoryId).OrderBy(x => x));
    }

    [Fact]
    public async Task UpdateTicketAsync_DoesNotChangeShiftDatePeriodOrDailySeq()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        var edit = MakeModel(new DateOnly(2099, 1, 1), ShiftPeriod.PM);
        var updated = await service.UpdateTicketAsync(ticket.Id, edit, "user-1", isAdmin: false);

        Assert.Equal(new DateOnly(2026, 9, 8), updated.ShiftDate);
        Assert.Equal(ShiftPeriod.AM, updated.ShiftPeriod);
        Assert.Equal(1, updated.DailySeq);
    }

    [Fact]
    public async Task UpdateTicketAsync_ByNonOwnerNonAdmin_Throws()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateTicketAsync(ticket.Id, MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-2", isAdmin: false));
    }

    [Fact]
    public async Task UpdateTicketAsync_ByAdmin_CanEditAnyonesTicket()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        var edit = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        edit.ProblemDescription = "管理員代改";
        var updated = await service.UpdateTicketAsync(ticket.Id, edit, "admin-1", isAdmin: true);

        Assert.Equal("管理員代改", updated.ProblemDescription);
        Assert.Equal("admin-1", updated.LastEditedByUserId);
    }

    [Fact]
    public async Task VoidTicketAsync_ByOwner_MarksVoidedWithAuditFields()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        await service.VoidTicketAsync(ticket.Id, "user-1", isAdmin: false);

        var saved = await _db.Tickets.FirstAsync(t => t.Id == ticket.Id);
        Assert.True(saved.IsVoided);
        Assert.Equal("user-1", saved.VoidedByUserId);
        Assert.NotNull(saved.VoidedAt);
    }

    [Fact]
    public async Task VoidTicketAsync_ByNonOwnerNonAdmin_Throws()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidTicketAsync(ticket.Id, "user-2", isAdmin: false));
    }

    [Fact]
    public async Task VoidTicketAsync_ByAdmin_CanVoidAnyonesTicket()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");

        await service.VoidTicketAsync(ticket.Id, "admin-1", isAdmin: true);

        var saved = await _db.Tickets.FirstAsync(t => t.Id == ticket.Id);
        Assert.True(saved.IsVoided);
        Assert.Equal("admin-1", saved.VoidedByUserId);
    }

    [Fact]
    public async Task GetTicketsAsync_ExcludesVoidedTicketsByDefault()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");
        await service.VoidTicketAsync(ticket.Id, "user-1", isAdmin: false);

        var results = await service.GetTicketsAsync(new TicketFilter());

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTicketsAsync_IncludesVoidedTickets_WhenRequested()
    {
        var service = new TicketService(_db);
        var ticket = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");
        await service.VoidTicketAsync(ticket.Id, "user-1", isAdmin: false);

        var results = await service.GetTicketsAsync(new TicketFilter { IncludeVoided = true });

        Assert.Single(results);
    }
}
