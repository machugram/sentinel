using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class CalendarsViewModel : ViewModelBase
{
    private readonly ICalendarService? _calendarService;

    [ObservableProperty] private ObservableCollection<TradingCalendar> _calendars = new();
    [ObservableProperty] private TradingCalendar? _selectedCalendar;
    [ObservableProperty] private bool _isMarketOpen;
    [ObservableProperty] private string _currentSessionName = "Closed";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _draftSessionName = string.Empty;
    [ObservableProperty] private string _draftSessionRegion = "Americas";
    [ObservableProperty] private string _draftSessionOpen = "09:30";
    [ObservableProperty] private string _draftSessionClose = "16:00";
    [ObservableProperty] private string _draftHolidayName = string.Empty;
    [ObservableProperty] private string _draftHolidayDate = DateTime.Today.ToString("yyyy-MM-dd");
    [ObservableProperty] private string _draftHolidayType = "Full";
    [ObservableProperty] private string _draftWindowDescription = string.Empty;
    [ObservableProperty] private string _draftWindowStart = DateTime.UtcNow.Date.AddDays(1).AddHours(2).ToString("yyyy-MM-dd HH:mm");
    [ObservableProperty] private string _draftWindowEnd = DateTime.UtcNow.Date.AddDays(1).AddHours(4).ToString("yyyy-MM-dd HH:mm");
    [ObservableProperty] private string? _editorError;

    public ObservableCollection<TradingSession> Sessions { get; } = new();
    public ObservableCollection<Holiday> Holidays { get; } = new();
    public ObservableCollection<MaintenanceWindow> MaintenanceWindows { get; } = new();
    public IReadOnlyList<string> RegionOptions { get; } = ["Americas", "EMEA", "APAC"];
    public IReadOnlyList<string> HolidayTypes { get; } = ["Full", "EarlyClose", "LateOpen"];

    public CalendarsViewModel()
    {
        Calendars = new ObservableCollection<TradingCalendar>
        {
            new()
            {
                Name = "Global Follow-the-Sun",
                Description = "Americas, EMEA, and APAC cash-equity sessions.",
                TimeZone = "UTC",
                Sessions =
                {
                    new TradingSession { Name = "APAC Cash", Region = TradingRegion.APAC, OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(8, 0) },
                    new TradingSession { Name = "EMEA Cash", Region = TradingRegion.EMEA, OpenTime = new TimeOnly(8, 0), CloseTime = new TimeOnly(16, 30) }
                },
                Holidays = { new Holiday { Date = new DateOnly(DateTime.UtcNow.Year, 1, 1), Name = "New Year's Day", Type = HolidayType.Full } }
            }
        };
        SelectedCalendar = Calendars.First();
        ShowCalendar(SelectedCalendar);
    }

    public CalendarsViewModel(ICalendarService calendarService)
    {
        _calendarService = calendarService;
        _ = LoadAsync();
    }

    partial void OnSelectedCalendarChanged(TradingCalendar? value)
    {
        if (value is not null)
            _ = ShowCalendarAsync(value);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_calendarService is null)
            return;
        IsLoading = true;
        try
        {
            Calendars = new ObservableCollection<TradingCalendar>((await _calendarService.GetAllCalendarsAsync()).ToList());
            SelectedCalendar = Calendars.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ShowCalendarAsync(TradingCalendar calendar)
    {
        ShowCalendar(calendar);
        if (_calendarService is null)
            return;

        IsMarketOpen = await _calendarService.IsMarketOpenAsync(calendar.Id, DateTime.UtcNow);
        var session = await _calendarService.GetCurrentSessionAsync(calendar.Id);
        CurrentSessionName = session?.Name ?? "Closed";
    }

    private void ShowCalendar(TradingCalendar calendar)
    {
        Sessions.Clear();
        foreach (var session in calendar.Sessions)
            Sessions.Add(session);
        Holidays.Clear();
        foreach (var holiday in calendar.Holidays)
            Holidays.Add(holiday);
        MaintenanceWindows.Clear();
        foreach (var window in calendar.MaintenanceWindows)
            MaintenanceWindows.Add(window);
        EditorError = null;
    }

    [RelayCommand]
    private void AddSession()
    {
        if (string.IsNullOrWhiteSpace(DraftSessionName))
        {
            EditorError = "Session name is required.";
            return;
        }

        if (!TimeOnly.TryParse(DraftSessionOpen, out var open) || !TimeOnly.TryParse(DraftSessionClose, out var close))
        {
            EditorError = "Use session times like 09:30 and 16:00.";
            return;
        }

        if (!Enum.TryParse<TradingRegion>(DraftSessionRegion, out var region))
            region = TradingRegion.Americas;

        Sessions.Add(new TradingSession
        {
            Id = Guid.NewGuid(),
            Name = DraftSessionName.Trim(),
            Region = region,
            OpenTime = open,
            CloseTime = close,
            TimeZone = SelectedCalendar?.TimeZone ?? "UTC"
        });
        DraftSessionName = string.Empty;
        EditorError = null;
    }

    [RelayCommand]
    private void RemoveSession(TradingSession? session)
    {
        if (session is not null)
            Sessions.Remove(session);
    }

    [RelayCommand]
    private void AddHoliday()
    {
        if (string.IsNullOrWhiteSpace(DraftHolidayName))
        {
            EditorError = "Holiday name is required.";
            return;
        }

        if (!DateOnly.TryParse(DraftHolidayDate, out var date))
        {
            EditorError = "Use a holiday date like 2026-12-25.";
            return;
        }

        if (!Enum.TryParse<HolidayType>(DraftHolidayType, out var type))
            type = HolidayType.Full;

        Holidays.Add(new Holiday
        {
            Date = date,
            Name = DraftHolidayName.Trim(),
            Type = type
        });
        DraftHolidayName = string.Empty;
        EditorError = null;
    }

    [RelayCommand]
    private void RemoveHoliday(Holiday? holiday)
    {
        if (holiday is not null)
            Holidays.Remove(holiday);
    }

    [RelayCommand]
    private void AddMaintenance()
    {
        if (string.IsNullOrWhiteSpace(DraftWindowDescription))
        {
            EditorError = "Maintenance description is required.";
            return;
        }

        if (!DateTime.TryParse(DraftWindowStart, out var start) || !DateTime.TryParse(DraftWindowEnd, out var end))
        {
            EditorError = "Use maintenance times like 2026-09-01 02:00.";
            return;
        }

        MaintenanceWindows.Add(new MaintenanceWindow
        {
            Id = Guid.NewGuid(),
            Description = DraftWindowDescription.Trim(),
            StartTime = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            EndTime = DateTime.SpecifyKind(end, DateTimeKind.Utc)
        });
        DraftWindowDescription = string.Empty;
        EditorError = null;
    }

    [RelayCommand]
    private void RemoveMaintenance(MaintenanceWindow? window)
    {
        if (window is not null)
            MaintenanceWindows.Remove(window);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedCalendar is null)
            return;

        SelectedCalendar.Sessions = Sessions.ToList();
        SelectedCalendar.Holidays = Holidays.ToList();
        SelectedCalendar.MaintenanceWindows = MaintenanceWindows.ToList();

        if (_calendarService is not null)
            await _calendarService.UpdateCalendarAsync(SelectedCalendar);

        WeakReferenceMessenger.Default.Send(new StatusMessage($"Saved {SelectedCalendar.Name}"));
        EditorError = null;
    }
}
