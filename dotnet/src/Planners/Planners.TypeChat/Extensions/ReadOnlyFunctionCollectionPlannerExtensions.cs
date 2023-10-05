// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Memory;

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel.Planners;
#pragma warning restore IDE0130

/// <summary>
/// Provides extension methods for the <see cref="IReadOnlyFunctionCollection"/> implementations for planners.
/// </summary>
public static class ReadOnlyFunctionCollectionPlannerExtensions
{
    /// <summary>
    /// Returns a string containing the manual for all available functions.
    /// </summary>
    /// <param name="functions">The function provider.</param>
    /// <param name="config">The planner config.</param>
    /// <param name="semanticQuery">The semantic query for finding relevant registered functions</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A string containing the manual for all available functions.</returns>
    public static async Task<string> GetFunctionsTypeChatManualAsync(
        this IReadOnlyFunctionCollection functions,
        PlannerConfigBase config,
        string? semanticQuery = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        IOrderedEnumerable<FunctionView> availableFunctions = await functions.GetFunctionsAsync(config, semanticQuery, logger, cancellationToken).ConfigureAwait(false);

        // TYPECHAT

        return string.Join("\n\n", availableFunctions.Select(x => x.ToManualString()));
    }
}
