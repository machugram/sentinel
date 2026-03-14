namespace Sentinel.Core.Models;

/// <summary>
/// Represents a trading session calendar for 24/7 trading support.
/// Based on PRD §5.2 - Session-aware calendars for trading sessions, holidays, CCP cut-offs.
/// </summary>
public class TradingCalendar
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public List<TradingSession> Sessions { get; set; } = new();
    public List<Holiday> Holidays { get; set; } = new();
    public List<MaintenanceWindow> MaintenanceWindows { get; set; } = new();
}

/// <summary>
/// Represents a trading session (e.g., Americas, EMEA, APAC).
/// </summary>
public class TradingSession
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TradingRegion Region { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public DayOfWeek[] TradingDays { get; set; } = new[] 
    { 
        DayOfWeek.Monday, 
        DayOfWeek.Tuesday, 
        DayOfWeek.Wednesday, 
        DayOfWeek.Thursday, 
        DayOfWeek.Friday 
    };
    public string TimeZone { get; set; } = "UTC";
}

/// <summary>
/// Represents a market holiday.
/// </summary>
public class Holiday
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public HolidayType Type { get; set; }
    public string[] AffectedMarkets { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents a scheduled maintenance window.
/// </summary>
public class MaintenanceWindow
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public string? RecurrenceRule { get; set; }
}

public enum TradingRegion
{
    Americas,
    EMEA,
    APAC
}

public enum HolidayType
{
    Full,
    EarlyClose,
    LateOpen
}
