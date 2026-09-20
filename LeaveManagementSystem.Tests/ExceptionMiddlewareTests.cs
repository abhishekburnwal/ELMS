using System.Security.Claims;
using LeaveManagementSystem.Middleware;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LeaveManagementSystem.Tests;

// ELMS-21 — GlobalExceptionMiddleware: full-field logging + persistence +
// rethrow, without touching the response. No database (fake store), SQL Server
// Express untouched.
public class ExceptionMiddlewareTests
{
    private sealed class FakeExceptionLogStore : IExceptionLogStore
    {
        public readonly List<ExceptionLog> Saved = new();
        public bool ThrowOnSave { get; set; }

        public Task SaveAsync(ExceptionLog entry, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("database is down");
            }

            Saved.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<(LogLevel Level, string Message, Exception? Exception)> Entries = new();

        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;

        public FakeScopeFactory(IServiceProvider provider) => _provider = provider;

        public IServiceScope CreateScope() => new FakeScope(_provider);

        private sealed class FakeScope : IServiceScope
        {
            public FakeScope(IServiceProvider provider) => ServiceProvider = provider;
            public IServiceProvider ServiceProvider { get; }
            public void Dispose() { }
        }
    }

    private static (GlobalExceptionMiddleware Middleware, FakeExceptionLogStore Store, CapturingLogger<GlobalExceptionMiddleware> Logger) Create(
        RequestDelegate next, FakeExceptionLogStore? store = null)
    {
        store ??= new FakeExceptionLogStore();
        var logger = new CapturingLogger<GlobalExceptionMiddleware>();
        var provider = new ServiceCollection()
            .AddSingleton<IExceptionLogStore>(store)
            .BuildServiceProvider();
        var middleware = new GlobalExceptionMiddleware(next, logger, new FakeScopeFactory(provider));
        return (middleware, store, logger);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "test-correlation-1";
        context.Request.Method = "POST";
        context.Request.Path = "/Employee/Apply";
        context.Request.QueryString = new QueryString("?probe=1");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim(ClaimTypes.Name, "Demo Employee")
        }, "test"));
        return context;
    }

    [Fact]
    public async Task LogsFullEntryPersistsAndRethrowsSameException()
    {
        var thrown = new InvalidOperationException("boom-probe");
        var (middleware, store, logger) = Create(_ => throw thrown);
        var context = AuthenticatedContext();

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(thrown, caught);

        var saved = Assert.Single(store.Saved);
        Assert.Equal("POST", saved.HttpMethod);
        Assert.Equal("/Employee/Apply?probe=1", saved.Path);
        Assert.Equal(2, saved.UserId);
        Assert.Equal("Demo Employee", saved.Username);
        Assert.Contains(nameof(InvalidOperationException), saved.ExceptionType);
        Assert.Equal("boom-probe", saved.Message);
        Assert.NotNull(saved.StackTrace);
        Assert.Equal("test-correlation-1", saved.CorrelationId);
        Assert.Equal(DateTimeKind.Utc, saved.Timestamp.Kind);
        Assert.True((DateTime.UtcNow - saved.Timestamp).TotalMinutes < 5);

        var error = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Error));
        Assert.Same(thrown, error.Exception);
        Assert.Contains("POST", error.Message);
        Assert.Contains("/Employee/Apply?probe=1", error.Message);
        Assert.Contains("test-correlation-1", error.Message);
        Assert.Contains("boom-probe", error.Message);
    }

    [Fact]
    public async Task PassesThroughWhenNoException()
    {
        var ran = false;
        var (middleware, store, logger) = Create(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.True(ran);
        Assert.Empty(store.Saved);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task StillRethrowsOriginalWhenStoreFails()
    {
        var thrown = new InvalidOperationException("original");
        var store = new FakeExceptionLogStore { ThrowOnSave = true };
        var (middleware, _, logger) = Create(_ => throw thrown, store);

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Same(thrown, caught);
        Assert.Empty(store.Saved);
        // Original error logged, plus a warning that persistence failed.
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception == thrown);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AnonymousRequestLogsNullUserFields()
    {
        var (middleware, store, _) = Create(_ => throw new InvalidOperationException("anon-boom"));
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "anon-correlation-9";
        context.Request.Method = "GET";
        context.Request.Path = "/Home/Index";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var saved = Assert.Single(store.Saved);
        Assert.Null(saved.UserId);
        Assert.Null(saved.Username);
        Assert.Equal("GET", saved.HttpMethod);
        Assert.Equal("/Home/Index", saved.Path);
        Assert.Equal("anon-correlation-9", saved.CorrelationId);
    }
}
