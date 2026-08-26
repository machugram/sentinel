using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Mock;

public sealed class MockCalendarService : ICalendarService
{
    private readonly MockDataStore _store;

    public MockCalendarService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<TradingCalendar>> GetAllCalendarsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<TradingCalendar>>(_store.Calendars.ToList());

    public Task<TradingCalendar?> GetCalendarByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Calendars.FirstOrDefault(c => c.Id == id));

    public Task<TradingCalendar> CreateCalendarAsync(TradingCalendar calendar, CancellationToken cancellationToken = default)
    {
        if (calendar.Id == Guid.Empty)
            calendar.Id = Guid.NewGuid();
        _store.Calendars.Add(calendar);
        return Task.FromResult(calendar);
    }

    public Task<TradingCalendar> UpdateCalendarAsync(TradingCalendar calendar, CancellationToken cancellationToken = default)
    {
        var existing = _store.Calendars.FirstOrDefault(c => c.Id == calendar.Id)
            ?? throw new InvalidOperationException($"Calendar {calendar.Id} not found");
        var index = _store.Calendars.IndexOf(existing);
        _store.Calendars[index] = calendar;
        return Task.FromResult(calendar);
    }

    public Task DeleteCalendarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = _store.Calendars.FirstOrDefault(c => c.Id == id);
        if (existing != null)
            _store.Calendars.Remove(existing);
        return Task.CompletedTask;
    }

    public Task<bool> IsMarketOpenAsync(Guid calendarId, DateTime dateTime, CancellationToken cancellationToken = default)
    {
        var calendar = _store.Calendars.FirstOrDefault(c => c.Id == calendarId);
        if (calendar is null)
            return Task.FromResult(false);

        if (calendar.Holidays.Any(h => h.Date == DateOnly.FromDateTime(dateTime) && h.Type == HolidayType.Full))
            return Task.FromResult(false);

        var time = TimeOnly.FromDateTime(dateTime);
        var open = calendar.Sessions.Any(s =>
            s.TradingDays.Contains(dateTime.DayOfWeek) && time >= s.OpenTime && time < s.CloseTime);
        return Task.FromResult(open);
    }

    public Task<TradingSession?> GetCurrentSessionAsync(Guid calendarId, CancellationToken cancellationToken = default)
    {
        var calendar = _store.Calendars.FirstOrDefault(c => c.Id == calendarId);
        if (calendar is null)
            return Task.FromResult<TradingSession?>(null);

        var now = DateTime.UtcNow;
        var time = TimeOnly.FromDateTime(now);
        var session = calendar.Sessions.FirstOrDefault(s =>
            s.TradingDays.Contains(now.DayOfWeek) && time >= s.OpenTime && time < s.CloseTime);
        return Task.FromResult(session);
    }
}
