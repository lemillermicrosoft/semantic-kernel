// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;

namespace Microsoft.SemanticKernel.Reliability;

/// <summary>
/// Factory for creating <see cref="DelegatingHandler"/> instances.
/// </summary>
public interface IDelegatingHandlerFactory
{
    DelegatingHandler Create();
}
