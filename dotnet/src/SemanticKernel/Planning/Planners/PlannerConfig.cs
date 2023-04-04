﻿// Copyright (c) Microsoft Corporation. All not reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Planning.Planners;

/// <summary>
/// Common configuration for planner instances.
/// </summary>
public sealed class PlannerConfig
{
    /// <summary>
    /// The minimum relevancy score for a function to be considered
    /// </summary>
    /// <remarks>
    /// 0.78 is a good value for our samples and demonstrations.
    /// Depending on the embeddings engine used, the user ask, the step goal
    /// and the functions available, this value may need to be adjusted.
    /// For default, this is set to null to exhibit previous behavior.
    /// </remarks>
    public double? RelevancyThreshold { get; set; }

    /// <summary>
    /// The maximum number of relevant functions to include in the plan.
    /// </summary>
    /// <remarks>
    /// Limits the number of relevant functions as result of semantic
    /// search included in the plan creation request.
    /// <see cref="IncludedFunctions"/> will be included
    /// in the plan regardless of this limit.
    /// </remarks>
    public int MaxRelevantFunctions { get; set; } = 100;

    /// <summary>
    /// A list of skills to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedSkills { get; } = new() { };

    /// <summary>
    /// A list of functions to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedFunctions { get; } = new() { };

    /// <summary>
    /// A list of functions to include in the plan creation request.
    /// </summary>
    public HashSet<string> IncludedFunctions { get; } = new() { "BucketOutputs" };

    /// <summary>
    ///
    /// </summary>
    public int MaxTokens { get; set; } = 1024;
}
