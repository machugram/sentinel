using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class CalendarsViewModel : ViewModelBase
{
    private readonly ICalendarService? _calendarService;

    [ObservableProperty] private ObservableCollection<TradingCalendar> _calendars = new();
    [ObservableProperty] private TradingCalendar? _selectedCalendar;
    [ObservableProperty] private bool _isMarketOpen;
    [ObservableProperty] private string _currentSessionName = "Closed";
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<TradingSession> Sessions { get; } = new();
    public ObservableCollection<Holiday> Holidays { get; } = new();
    public ObservableCollection<MaintenanceWindow> MaintenanceWindows { get; } = new();

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
    }
}
