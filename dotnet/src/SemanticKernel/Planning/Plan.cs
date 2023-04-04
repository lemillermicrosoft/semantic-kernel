// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Planning;

/// <summary>
/// A plan class that is executable by the kernel
/// </summary>
public class Plan : ISKFunction
{
    protected ISKFunction? Function { get; set; } = null;

    public void SetFunction(ISKFunction function)
    {
        this.Function = function;
        this.Name = function.Name;
        this.SkillName = function.SkillName;
        this.Description = function.Description;
        this.IsSemantic = function.IsSemantic;
        this.RequestSettings = function.RequestSettings;
    }

    public static Plan FromISKFunction(ISKFunction function)
    {
        var plan = new Plan();

        plan.SetFunction(function);

        return plan;
    }

    /// <summary>
    /// State of the plan
    /// </summary>
    [JsonPropertyName("state")]
    public ContextVariables State { get; } = new();

    /// <summary>
    /// Steps of the plan
    /// </summary>
    [JsonPropertyName("steps")]
    public List<Plan> Steps { get; } = new();

    /// <summary>
    /// Named parameters for the function
    /// </summary>
    [JsonPropertyName("named_parameters")]
    public ContextVariables NamedParameters { get; set; } = new();

    /// <summary>
    /// Run the next step of the plan
    /// </summary>
    /// <param name="kernel">Kernel instance to use</param>
    /// <param name="variables">Variables to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated plan</returns>
    public virtual Task<Plan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
    {
        // no-op, return self
        return Task.FromResult<Plan>(this);
    }

    // ISKFunction implementation

    /// <inheritdoc/>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("skill_name")]
    public string SkillName { get; set; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("is_semantic")]
    public bool IsSemantic { get; protected set; } = false;

    /// <inheritdoc/>
    [JsonPropertyName("request_settings")]
    public CompleteRequestSettings RequestSettings { get; protected set; } = new();

    /// <inheritdoc/>
    public FunctionView Describe()
    {
        return this.Function?.Describe() ?? new(); // todo new()???
    }

    /// <inheritdoc/>
    public virtual Task<SKContext> InvokeAsync(string input, SKContext? context = null, CompleteRequestSettings? settings = null, ILogger? log = null, CancellationToken? cancel = null)
    {
        return this.Function is null
            ? throw new NotImplementedException()
            : this.Function.InvokeAsync(input, context, settings, log, cancel);
    }

    /// <inheritdoc/>
    public virtual Task<SKContext> InvokeAsync(SKContext? context = null, CompleteRequestSettings? settings = null, ILogger? log = null, CancellationToken? cancel = null)
    {
        return this.Function is null
            ? throw new NotImplementedException()
            : this.Function.InvokeAsync(context, settings, log, cancel);
    }

    /// <inheritdoc/>
    public ISKFunction SetDefaultSkillCollection(IReadOnlySkillCollection skills)
    {
        return this.Function is null
            ? throw new NotImplementedException()
            : this.Function.SetDefaultSkillCollection(skills);
    }

    /// <inheritdoc/>
    public ISKFunction SetAIService(Func<ITextCompletion> serviceFactory)
    {
        return this.Function is null
            ? throw new NotImplementedException()
            : this.Function.SetAIService(serviceFactory);
    }

    /// <inheritdoc/>
    public ISKFunction SetAIConfiguration(CompleteRequestSettings settings)
    {
        return this.Function is null
            ? throw new NotImplementedException()
            : this.Function.SetAIConfiguration(settings);
        // todo change my settings? Maybe we just have those properties reference the instance in the function?
    }
}
