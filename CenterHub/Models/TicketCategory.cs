namespace CenterHub.Models;

public class TicketCategory
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int ServiceCategoryId { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = null!;
}
