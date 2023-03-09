// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Reliability;

/// <summary>
/// A retry mechanism that does not retry.
/// </summary>
public class NullRetryPolicy : IHttpRetryPolicy
{
    /// <summary>
    /// Executes the given action with retry logic.
    /// </summary>
    /// <param name="request">The request to retry on error response.</param>
    /// <param name="log">The logger to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An awaitable task.</returns>
    public Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> request, ILogger log, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request();
    }
}
