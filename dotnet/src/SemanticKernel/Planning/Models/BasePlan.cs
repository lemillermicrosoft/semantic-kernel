// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning.Models;

public class BasePlan : IPlan
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("context_variables")]
    public ContextVariables State { get; set; } = new();

    public Task<IPlan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }
}
