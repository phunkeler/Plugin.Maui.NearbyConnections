using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>
/// Routes plugin <see cref="ILogger"/> output to one test's xUnit output, so each line lands in
/// that test's <c>StdOut</c> node in the TRX. Each line is one JSON object carrying the level,
/// category, EventId, thread, formatted message, and the named fields of the source-generated
/// log message, so a failing run can be queried rather than read.
/// </summary>
/// <param name="output">The output helper of the test this provider belongs to.</param>
/// <remarks>
/// <para>
/// Takes the helper by constructor rather than reading <c>TestContext.Current</c> at write time.
/// The ambient lookup flows through the execution context, and this plugin's platform callbacks
/// arrive on the iOS delegate's private serial queue and on Android's GMS callback threads, which
/// are outside it. There the ambient value is <see langword="null"/> and the line is silently
/// dropped. Holding the helper in a field is what lets a callback-thread line reach the test.
/// </para>
/// <para>
/// Console logging cannot do this at all: it writes to the process's shared stdout, which xUnit
/// cannot attribute to a test, so the per-test <c>StdOut</c> nodes come back empty.
/// </para>
/// </remarks>
sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    readonly Lock _gate = new();
    ITestOutputHelper? _output = output;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(this, categoryName);

    /// <summary>
    /// Stops routing output, for callbacks that outlive the test. Writing to a finished test's
    /// helper throws, which would fail an unrelated test.
    /// </summary>
    internal void Detach()
    {
        lock (_gate)
        {
            _output = null;
        }
    }

    void Write(string line)
    {
        // Held for the whole write: the helper can be detached concurrently by Dispose while a
        // platform callback is mid-write on its own thread.
        lock (_gate)
        {
            try
            {
                _output?.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // The test finished between the null check and the write. Dropping the line is
                // correct: the alternative is failing an unrelated test from a late callback.
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Detach();

    sealed class TestOutputLogger(TestOutputLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // One JSON object per line. The source-generated [LoggerMessage] state carries the
            // message's named fields, so emitting them keeps Id/DisplayName/State queryable in the
            // TRX instead of flattened into prose.
            //
            // Written with Utf8JsonWriter, not JsonSerializer: the runner app is trimmed, and the
            // reflection-based overload fails the build with IL2026. Every value here is already a
            // string, so a source-generated context would buy nothing.
            var buffer = new ArrayBufferWriter<byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("level", Abbreviate(logLevel));
                json.WriteString("category", category);
                json.WriteNumber("eventId", eventId.Id);

                if (eventId.Name is { } eventName)
                {
                    json.WriteString("eventName", eventName);
                }

                json.WriteNumber("thread", Environment.CurrentManagedThreadId);
                json.WriteString("message", formatter(state, exception));

                if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
                {
                    foreach (var pair in pairs)
                    {
                        // {OriginalFormat} is the template, already covered by "message".
                        if (pair.Key != "{OriginalFormat}")
                        {
                            json.WriteString(pair.Key, pair.Value?.ToString());
                        }
                    }
                }

                if (exception is not null)
                {
                    json.WriteString("exception", exception.ToString());
                }

                json.WriteEndObject();
            }

            provider.Write(Encoding.UTF8.GetString(buffer.WrittenSpan));
        }

        static string Abbreviate(LogLevel level) => level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none",
        };
    }
}
