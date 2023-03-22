// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Planning;

public interface IPlan
{
    string Goal { get; }

    ContextVariables State { get; }

    // TODO Serialize?
    string ToPlanString();
}

public interface IPlanWithSteps : IPlan
{
    IList<PlanStep> Steps { get; }
}


public class BasePlan : IPlan
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("context_variables")]
    public ContextVariables State { get; set; } = new(); // TODO Serializing this doesn't work most likely

    public static IPlan FromString(string planString)
    {
        return Json.Deserialize<BasePlan>(planString) ?? new BasePlan(); // TODO
    }

    public virtual string ToPlanString()
    {
        return Json.Serialize(this);
    }
}

public class SimplePlan : BasePlan, IPlanWithSteps
{
    [JsonPropertyName("steps")]
    List<PlanStep> steps { get; set; } = new();

    public IList<PlanStep> Steps => steps;

    public override string ToPlanString()
    {
        return Json.Serialize(this);
    }

    new public static IPlan FromString(string planString)
    {
        return Json.Deserialize<SimplePlan>(planString) ?? new SimplePlan(); // TODO
    }
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
    public ContextVariables NamedParameters { get; set; } = new(); // TODO Parameter?

    [JsonPropertyName("manifests")]
    public FunctionView Manifests { get; set; } = new(); // TODO probably not FunctionView
}

public class Parameter
{
    // String Description

    // Bool Required

    // <T> Content type

    // T defaultValue

    // T value

    // [bool isSecret]

    // [bool isPII]
}
