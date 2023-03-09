// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Configuration;

namespace Microsoft.SemanticKernel.Reliability;

internal class DefaultHttpRetryHandlerFactory : IDelegatingHandlerFactory
{
    internal DefaultHttpRetryHandlerFactory(KernelConfig.HttpRetryConfig? config = null, ILogger? log = null)
    {
        this._config = config;
        this._log = log;
    }

    public DelegatingHandler Create()
    {
        return new DefaultHttpRetryHandler(this._config, this._log);
    }

    private readonly KernelConfig.HttpRetryConfig? _config;
    private readonly ILogger? _log;
}
