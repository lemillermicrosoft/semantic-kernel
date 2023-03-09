﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;

namespace Microsoft.SemanticKernel.Reliability;

public class NullHttpRetryHandlerFactory : IDelegatingHandlerFactory
{
    public DelegatingHandler Create()
    {
        return new NullHttpRetryHandler();
    }
}

/// <summary>
/// A http retry handler that does not retry.
/// </summary>
public class NullHttpRetryHandler : DelegatingHandler
{
}
