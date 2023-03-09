// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Microsoft.SemanticKernel.Configuration.KernelConfig;

namespace Microsoft.SemanticKernel.Reliability;

public class DefaultHttpRetryPolicy : IHttpRetryPolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultHttpRetryPolicy"/> class.
    /// </summary>
    /// <param name="config">The retry configuration.</param>
    public DefaultHttpRetryPolicy(HttpRetryConfig? config = null) : this(config!, null, null)
    {
        this._config = config ?? new HttpRetryConfig();
    }

    internal DefaultHttpRetryPolicy(HttpRetryConfig config, IDelayProvider? delayProvider = null, ITimeProvider? timeProvider = null)
    {
        this._config = config;
        this._delayProvider = delayProvider ?? new TaskDelayProvider();
        this._timeProvider = timeProvider ?? new DefaultTimeProvider();
    }

    /// <summary>
    /// Executes the action with retry logic
    /// </summary>
    /// <remark>
    /// The action is retried if it throws an exception that is not a critical exception and is a retryable error code.
    /// If the action throws an exception that is not a retryable error code, it is not retried.
    /// If the exception contains a RetryAfter header, the action is retried after the specified delay.
    /// If configured to use exponential backoff, the delay is doubled for each retry.
    /// If the action throws a critical exception, it is not retried.
    /// </remark>
    /// <param name="request">The request to execute</param>
    /// <param name="log">The logger to use</param>
    /// <param name="cancellationToken">The cancellation token</param>
    public async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> request, ILogger log,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;

        var start = this._timeProvider.GetCurrentTime();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan waitFor = default;
            string reason = string.Empty;
            HttpResponseMessage? response = null;
            try
            {
                response = await request();

                // If the request does not require a retry then we're done
                if (!this.ShouldRetry(response.StatusCode))
                {
                    return response;
                }

                // Drain response content to free connections. Need to perform this
                // before retry attempt and before the TooManyRetries ServiceException.
                if (response.Content != null)
                {
#if NET5_0_OR_GREATER
                    await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
                    await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
                }

                reason = response.StatusCode.ToString();

                if (retryCount >= this._config.MaxRetryCount)
                {
                    log.LogWarning(
                        "Error executing request, max retry count reached. Reason: {0}", reason);
                    return response;
                }

                // If the retry delay is longer than the total timeout, then we'll
                // just return
                if (!this.HasTimeForRetry(start, retryCount, response, out waitFor))
                {
                    log.LogWarning(
                        "Error executing request, max total retry time reached. Reason: {0}", reason);
                    return response;
                }
            }
            catch (Exception e) when ((this.ShouldRetry(e) || this.ShouldRetry(e.InnerException)) &&
                                      retryCount < this._config.MaxRetryCount &&
                                      this.HasTimeForRetry(start, retryCount, response, out waitFor))
            {
                reason = e.GetType().ToString();
            }

            // If the request requires a retry then we'll retry
            log.LogWarning(
                "Error executing action [attempt {0} of {1}]. Reason: {2}. Will retry after {3}ms",
                retryCount + 1,
                this._config.MaxRetryCount,
                reason,
                waitFor.TotalMilliseconds);

            // Increase retryCount
            retryCount++;

            await this._delayProvider.DelayAsync(waitFor, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetWaitTime(int retryCount, HttpResponseMessage? response)
    {
        var retryAfter = response?.Headers.RetryAfter?.Date.HasValue == true ? response?.Headers.RetryAfter?.Date - DateTimeOffset.Now : (response?.Headers.RetryAfter?.Delta) ?? this._config.MinRetryDelay;
        retryAfter ??= this._config.MinRetryDelay;

        var timeToWait = retryAfter > this._config.MaxRetryDelay
            ? this._config.MaxRetryDelay
            : retryAfter < this._config.MinRetryDelay
                ? this._config.MinRetryDelay
                : retryAfter ?? default;

        if (this._config.UseExponentialBackoff)
        {
            for (var backoffRetryCount = 1; backoffRetryCount < retryCount + 1; backoffRetryCount++)
            {
                timeToWait = timeToWait.Add(timeToWait);
            }
        }

        return timeToWait;
    }

    private bool HasTimeForRetry(DateTimeOffset start, int retryCount, HttpResponseMessage? response, out TimeSpan waitFor)
    {
        waitFor = this.GetWaitTime(retryCount, response);
        return this._timeProvider.GetCurrentTime() - start + waitFor < this._config.MaxTotalRetryTime;
    }

    private bool ShouldRetry(HttpStatusCode statusCode)
    {
        return this._config.RetryableStatusCodes.Contains(statusCode);
    }

    private bool ShouldRetry(Exception exception)
    {
        return this._config.RetryableExceptionTypes.Contains(exception.GetType());
    }

    private readonly HttpRetryConfig _config;
    private readonly IDelayProvider _delayProvider;
    private readonly ITimeProvider _timeProvider;

    internal interface IDelayProvider
    {
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    internal class TaskDelayProvider : IDelayProvider
    {
        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    internal interface ITimeProvider
    {
        DateTimeOffset GetCurrentTime();
    }

    internal class DefaultTimeProvider : ITimeProvider
    {
        public DateTimeOffset GetCurrentTime()
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
