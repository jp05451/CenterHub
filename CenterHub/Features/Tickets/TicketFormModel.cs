// CenterHub/Features/Tickets/TicketFormModel.cs
using System.ComponentModel.DataAnnotations;
using CenterHub.Models;

namespace CenterHub.Features.Tickets;

public class TicketFormModel
{
    [Required]
    public DateOnly ShiftDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Required]
    public ShiftPeriod ShiftPeriod { get; set; }

    [Required]
    public TimeOnly ContactTime { get; set; } = TimeOnly.FromDateTime(DateTime.Now);

    [Required(ErrorMessage = "請輸入申請單位")]
    public string RequestingUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入申請人")]
    public string RequesterName { get; set; } = string.Empty;

    public string? RequesterContact { get; set; }

    [Required]
    public ServiceMode ServiceMode { get; set; }

    [Required(ErrorMessage = "請至少選擇一個服務類別")]
    [MinLength(1, ErrorMessage = "請至少選擇一個服務類別")]
    public List<int> SelectedCategoryIds { get; set; } = new();

    [Required]
    public ResolutionStatus ResolutionStatus { get; set; }

    [Range(1, 5)]
    public int? SatisfactionRating { get; set; }

    [Range(0, 1440, ErrorMessage = "服務時間需介於 0 到 1440 分鐘")]
    public int ServiceDurationMinutes { get; set; }

    public string? OperatingSystem { get; set; }

    [Required(ErrorMessage = "請填寫問題簡述")]
    public string ProblemDescription { get; set; } = string.Empty;

    public string? SolutionDescription { get; set; }
}
