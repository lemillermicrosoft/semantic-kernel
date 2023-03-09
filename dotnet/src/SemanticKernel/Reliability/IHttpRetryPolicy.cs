// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Reliability;

/// <summary>
/// Interface for HTTP retry policy on AI calls.
/// </summary>
public interface IHttpRetryPolicy
{
    /// <summary>
    /// Executes an HTTP request with retry logic.
    /// </summary>
    /// <param name="request">The request to execute.</param>
    /// <param name="log">The logger to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response from the request.</returns>
    Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> request, ILogger log, CancellationToken cancellationToken = default);
}
