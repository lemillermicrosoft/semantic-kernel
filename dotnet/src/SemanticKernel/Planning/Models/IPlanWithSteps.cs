// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Planning.Models;

public interface IPlanWithSteps : IPlan
{
    IList<PlanStep> Steps { get; }
}

public partial class PlanStep
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("selected_skill")]
    public string SelectedSkill { get; set; } = string.Empty;

    [JsonPropertyName("selected_function")]
    public string SelectedFunction { get; set; } = string.Empty;

    // TODO - consider adding the relevancy score for functions added to manual based on relevancy

    [JsonPropertyName("named_parameters")]
    public ContextVariables NamedParameters { get; set; } = new();
    // TODO Parameter? Key Value Pair of a Parameter type? Is this connected to FunctionView or a subset of it?
    // Key is FunctionView Parameter and value is the value -- what about handling output from other functions early in the plan?
    // Pointer reference is OKAY in that case and substitution is done later at execution?

    [JsonPropertyName("manifests")]
    public FunctionView Manifests { get; set; } = new();
    // TODO probably not FunctionView -- what will this be used for, who needs to define it, will this include remote execution details?
}

public partial class Parameter
{
    // String Description

    // Bool Required

    // <T> Content type

    // T defaultValue

    // T value

    // [bool isSecret]

    // [bool isPII]
}
