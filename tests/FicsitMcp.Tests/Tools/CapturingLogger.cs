using Microsoft.Extensions.Logging;

namespace FicsitMcp.Tests.Tools;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> that captures every log entry at every level — the fully
/// formatted message, the raw state, and any exception — so a test can assert a secret appears in
/// NONE of them. Deliberately tiny (no extra package) and always enabled so nothing is filtered out.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<CapturedLogEntry> _entries = [];

    /// <summary>Every captured entry, in log order.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => _entries;

    /// <summary>
    /// The concatenation of every formatted message, state string, and exception string across all
    /// captured entries — the single haystack a leak test searches for a secret.
    /// </summary>
    public string AllText =>
        string.Join(
            "\n",
            _entries.Select(e => $"{e.Message}|{e.State}|{e.Exception?.ToString()}"));

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    // Always enabled: a leak test must see entries the production filter might otherwise drop.
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new CapturedLogEntry(
            logLevel,
            formatter(state, exception),
            state?.ToString(),
            exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>One captured log entry: level, formatted message, raw state string, and exception.</summary>
internal sealed record CapturedLogEntry(
    LogLevel Level,
    string Message,
    string? State,
    Exception? Exception);
