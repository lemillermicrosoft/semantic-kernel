// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RepoUtils;

/// <summary>
/// Basic logger printing to console
/// </summary>
internal static class ConsoleLogger
{
    internal static ILogger Log => LogFactory.CreateLogger("_");

    private static ILoggerFactory LogFactory => s_loggerFactory.Value;

    private static readonly Lazy<ILoggerFactory> s_loggerFactory = new(LogBuilder);

    private static ILoggerFactory LogBuilder()
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);

            // builder.AddFilter("Microsoft", LogLevel.Trace);
            // builder.AddFilter("Microsoft", LogLevel.Debug);
            // builder.AddFilter("Microsoft", LogLevel.Information);
            // builder.AddFilter("Microsoft", LogLevel.Warning);
            // builder.AddFilter("Microsoft", LogLevel.Error);

            // builder.AddFilter("Microsoft", LogLevel.Debug);
            // builder.AddFilter("System", LogLevel.Warning);

            // builder.AddConsole();
            builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
                .AddFile("")
                .AddFilter((provider, category, logLevel) =>
                {
                    // Category is *always* "app" here
                    if (provider.Contains("ConsoleLoggerProvider", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return logLevel >= LogLevel.Error;
                    }

                    return logLevel >= LogLevel.Debug;
                });
        }); //.AddFile("");

        return factory;
    }
}

internal static class FileLoggerExtensions
{
    public static ILoggerFactory AddFile(this ILoggerFactory factory)
    {
        factory.AddProvider(new FileLoggerProvider());
        return factory;
    }

    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        // builder.AddConfiguration();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());

        // builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<FilerLoggerOptions>, FilerLoggerOptionsSetup>());
        // builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IOptionsChangeTokenSource<LoggerOptions>, FileLoggerProviderOptionsChangeTokenSource<FilerLoggerOptions, FileLoggerProvider>>());

        return builder;
    }
}

internal sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    public IExternalScopeProvider ScopeProvider { get; set; }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // return logLevel == LogLevel.Information;
        return true;
    }

    public void Dispose()
    {
    }

    #region ISupportExternalScope Support

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.ScopeProvider = scopeProvider;
    }

    #endregion
}

internal sealed class FileLogger : ILogger
{
    public FileLoggerProvider Provider { get; private set; }
    public string Category { get; private set; }
    private string filePath = "";
    private static object _lock = new();
    private string fullFilePath;
    private string infoFilePath;

    public FileLogger(FileLoggerProvider provider, string category)
    {
        this.Provider = provider;
        this.Category = category;
        this.fullFilePath = System.IO.Path.Combine(this.filePath, DateTime.Now.ToString("yyyy-MM-dd_hh_mm") + ".log.txt");
        this.infoFilePath = System.IO.Path.Combine(this.filePath, DateTime.Now.ToString("yyyy-MM-dd_hh-mm") + ".log.info.txt");
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return this.Provider.ScopeProvider.Push(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        //return logLevel == LogLevel.Trace;
        // return true;
        return this.Provider.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
    {
        if (this.IsEnabled(logLevel))
        {
            if (formatter != null)
            {
                var scopeState = "";
                lock (_lock)
                {
                    this.Provider.ScopeProvider.ForEachScope((scope, loggingProps) =>
                    {
                        if (scope is string)
                        {
                            scopeState += scope.ToString();
                        }
                        else
                        {
                            scopeState += JsonSerializer.Serialize(scope);
                        }
                    }, state);

                    var n = Environment.NewLine;
                    string exc = "";
                    if (exception != null)
                    {
                        exc = n + exception.GetType() + ": " + exception.Message + n + exception.StackTrace + n;
                    }

                    var message = DateTime.Now.ToShortTimeString() + $" {scopeState} [" + logLevel.ToString() + "] " + formatter(state, exception) + n + exc;
                    if (scopeState == "MrklSystemPlanner")
                    {
                        System.IO.File.AppendAllText(this.fullFilePath, message);
                        // info and above to info file
                        if (logLevel >= LogLevel.Information)
                        {
                            System.IO.File.AppendAllText(this.infoFilePath, message);
                        }
                    }
                }
            }
        }
    }
}
