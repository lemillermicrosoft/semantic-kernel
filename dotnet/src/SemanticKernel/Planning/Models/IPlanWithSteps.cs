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

public class PlanStep
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("selected_skill")]
    public string SelectedSkill { get; set; } = string.Empty;

    [JsonPropertyName("selected_function")]
    public string SelectedFunction { get; set; } = string.Empty;

    [JsonPropertyName("named_parameters")]
    public ContextVariables NamedParameters { get; set; } = new();

    [JsonPropertyName("manifests")]
    public FunctionView Manifests { get; set; } = new();

    // TODO - consider adding the relevancy score for functions added to manual based on relevancy
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
