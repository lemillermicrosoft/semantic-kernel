// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning.Models;

public class BasePlan : IPlan
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("context_variables")]
    public ContextVariables State { get; set; } = new(); // TODO Serializing this doesn't work most likely

    // public static IPlan FromString(string planString)
    // {
    //     return Json.Deserialize<BasePlan>(planString) ?? new BasePlan(); // TODO
    // }

    // public virtual string ToPlanString()
    // {
    //     return Json.Serialize(this);
    // }
}
