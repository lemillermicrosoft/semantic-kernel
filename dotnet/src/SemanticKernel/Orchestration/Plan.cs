// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>
/// Standard Semantic Kernel callable plan.
/// Plan is used to create trees of <see cref="ISKFunction"/>s.
/// </summary>
public sealed class Plan : ISKFunction
{
    /// <summary>
    /// State of the plan
    /// </summary>
    [JsonPropertyName("state")]
    public ContextVariables State { get; } = new();

    /// <summary>
    /// Steps of the plan
    /// </summary>
    [JsonPropertyName("steps")]
    internal List<Plan> Steps { get; } = new();

    /// <summary>
    /// Named parameters for the function
    /// </summary>
    [JsonPropertyName("named_parameters")]
    public ContextVariables NamedParameters { get; set; } = new();

    /// <summary>
    /// Named outputs for the function
    /// </summary>
    [JsonPropertyName("named_outputs")]
    public ContextVariables NamedOutputs { get; set; } = new();

    public bool HasNextStep => this.NextStep < this.Steps.Count;

    public int NextStep { get; private set; } = 0;

    #region ISKFunction implementation

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
    public bool IsSemantic { get; internal set; } = false;

    /// <inheritdoc/>
    [JsonPropertyName("request_settings")]
    public CompleteRequestSettings RequestSettings { get; internal set; } = new();

    #endregion ISKFunction implementation

    /// <summary>
    /// Initializes a new instance of the <see cref="Plan"/> class with a goal description.
    /// </summary>
    /// <param name="goal">The goal of the plan used as description.</param>
    public Plan(string goal)
    {
        this.Description = goal;
        this.SkillName = this.GetType().FullName;
        this.Name = goal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Plan"/> class with a goal description and steps.
    /// </summary>
    /// <param name="goal">The goal of the plan used as description.</param>
    /// <param name="steps">The steps to add.</param>
    public Plan(string goal, params ISKFunction[] steps) : this(goal)
    {
        this.AddStep(steps);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Plan"/> class with a goal description and steps.
    /// </summary>
    /// <param name="goal">The goal of the plan used as description.</param>
    /// <param name="steps">The steps to add.</param>
    public Plan(string goal, params Plan[] steps) : this(goal)
    {
        this.AddStep(steps);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Plan"/> class with a function.
    /// </summary>
    /// <param name="function">The function to execute.</param>
    public Plan(ISKFunction function)
    {
        this.SetFunction(function);
    }

    /// <summary>
    /// Adds one or more existing plans to the end of the current plan as steps.
    /// </summary>
    /// <param name="steps">The plans to add as steps to the current plan.</param>
    /// <remarks>
    /// When you add a plan as a step to the current plan, the steps of the added plan are executed after the steps of the current plan have completed.
    /// </remarks>
    public void AddStep(params Plan[] steps)
    {
        this.Steps.AddRange(steps);
    }

    /// <summary>
    /// Adds one or more new steps to the end of the current plan.
    /// </summary>
    /// <param name="steps">The steps to add to the current plan.</param>
    /// <remarks>
    /// When you add a new step to the current plan, it is executed after the previous step in the plan has completed. Each step can be a function call or another plan.
    /// </remarks>
    public void AddStep(params ISKFunction[] steps)
    {
        foreach (var step in steps)
        {
            this.Steps.Add(new Plan(step));
        }
    }

    /// <summary>
    /// Runs the next step in the plan using the provided kernel instance and variables.
    /// </summary>
    /// <param name="kernel">The kernel instance to use for executing the plan.</param>
    /// <param name="variables">The variables to use for the execution of the plan.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the execution of the plan.</param>
    /// <returns>A task representing the asynchronous execution of the plan's next step.</returns>
    /// <remarks>
    /// This method executes the next step in the plan using the specified kernel instance and context variables. The context variables contain the necessary information for executing the plan, such as the memory, skills, and logger. The method returns a task representing the asynchronous execution of the plan's next step.
    /// </remarks>
    public Task<Plan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
    {
        var context = new SKContext(
            variables,
            kernel.Memory,
            kernel.Skills,
            kernel.Log,
            cancellationToken);
        return this.InvokeNextStepAsync(context);
    }

    #region ISKFunction implementation

    /// <inheritdoc/>
    public FunctionView Describe()
    {
        // TODO - Eventually, we should be able to describe a plan and it's expected inputs/outputs
        return this.Function?.Describe() ?? new();
    }

    /// <inheritdoc/>
    public Task<SKContext> InvokeAsync(string input, SKContext? context = null, CompleteRequestSettings? settings = null, ILogger? log = null,
        CancellationToken? cancel = null)
    {
        context ??= new SKContext(new ContextVariables(input), null!, null, log ?? NullLogger.Instance, cancel ?? CancellationToken.None);
        return this.InvokeAsync(context, settings, log, cancel);
    }

    /// <inheritdoc/>
    public async Task<SKContext> InvokeAsync(SKContext? context = null, CompleteRequestSettings? settings = null, ILogger? log = null,
        CancellationToken? cancel = null)
    {
        context ??= new SKContext(new ContextVariables(), null!, null, log ?? NullLogger.Instance, cancel ?? CancellationToken.None);

        if (this.Function is not null)
        {
            var result = await this.Function.InvokeAsync(context, settings, log, cancel);

            if (result.ErrorOccurred)
            {
                result.Log.LogError(
                    result.LastException,
                    "Something went wrong in plan step {0}.{1}:'{2}'", this.SkillName, this.Name, context.LastErrorDescription);
                return result;
            }

            context.Variables.Update(result.Result.ToString());
        }
        else
        {
            // loop through steps and execute until completion
            while (this.HasNextStep)
            {
                var functionContext = context;

                // Loop through State and add anything missing to functionContext
                foreach (var item in this.State)
                {
                    if (!functionContext.Variables.ContainsKey(item.Key))
                    {
                        functionContext.Variables.Set(item.Key, item.Value);
                    }
                }

                await this.InvokeNextStepAsync(functionContext);

                context.Variables.Update(this.State.ToString());
            }
        }

        return context;
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
    }

    #endregion ISKFunction implementation

    /// <summary>
    /// Invoke the next step of the plan
    /// </summary>
    /// <param name="context">Context to use</param>
    /// <returns>The updated plan</returns>
    /// <exception cref="KernelException">If an error occurs while running the plan</exception>
    public async Task<Plan> InvokeNextStepAsync(SKContext context)
    {
        if (this.HasNextStep)
        {
            var step = this.Steps[this.NextStep];

            var functionVariables = this.GetNextStepVariables(context.Variables, step);
            var functionContext = new SKContext(functionVariables, context.Memory, context.Skills, context.Log, context.CancellationToken);

            var keysToIgnore = functionVariables.Select(x => x.Key).ToList(); // when will there be new keys added? that's output kind of?

            var result = await step.InvokeAsync(functionContext);

            if (result.ErrorOccurred)
            {
                throw new KernelException(KernelException.ErrorCodes.FunctionInvokeError,
                    $"Error occurred while running plan step: {context.LastErrorDescription}", context.LastException);
            }

            #region Update State
            // TODO What does this do?
            foreach (var (key, _) in functionVariables)
            {
                if (!keysToIgnore.Contains(key, StringComparer.InvariantCultureIgnoreCase) && functionVariables.Get(key, out var value))
                {
                    this.State.Set(key, value);
                }
            }

            // TODO Handle outputs
            this.State.Update(result.Result.Trim()); // default

            foreach (var item in step.NamedOutputs)
            {
                if (string.IsNullOrEmpty(item.Key) || item.Key.ToUpperInvariant() == "INPUT" || string.IsNullOrEmpty(item.Value))
                {
                    continue;
                }

                if (item.Key.ToUpperInvariant() == "RESULT")
                {
                    this.State.Set(item.Value, result.Result.Trim());
                }
                else if (result.Variables.Get(item.Key, out var value))
                {
                    this.State.Set(item.Value, value); // iffy on this one...
                }
            }
            // if (string.IsNullOrEmpty(sequentialPlan.OutputKey))
            // {
            //     _ = this.State.Update(result.Result.Trim());
            // }
            // else
            // {
            //     this.State.Set(sequentialPlan.OutputKey, result.Result.Trim());
            // }

            // _ = this.State.Update(result.Result.Trim());
            // if (!string.IsNullOrEmpty(sequentialPlan.OutputKey))
            // {
            //     this.State.Set(sequentialPlan.OutputKey, result.Result.Trim());
            // }

            // if (!string.IsNullOrEmpty(sequentialPlan.ResultKey))
            // {
            //     _ = this.State.Get(SkillPlan.ResultKey, out var resultsSoFar);
            //     this.State.Set(SkillPlan.ResultKey,
            //         string.Join(Environment.NewLine + Environment.NewLine, resultsSoFar, result.Result.Trim()));
            // }

            #endregion Update State

            this.NextStep++;
        }

        return this;
    }

    private ContextVariables GetNextStepVariables(ContextVariables variables, Plan step)
    {
        // Initialize function-scoped ContextVariables
        // Default input should be the Input from the SKContext, or the Input from the Plan.State, or the Plan.Goal
        var planInput = string.IsNullOrEmpty(variables.Input) ? this.State.Input : variables.Input;
        var functionInput = string.IsNullOrEmpty(planInput) ? (this.Description ?? string.Empty) : planInput;
        var functionVariables = new ContextVariables(functionInput);

        // When I execute a plan, it has a State, ContextVariables, and a Goal

        // Priority for functionVariables is:
        // - NamedParameters (pull from State by a key value)
        // - Parameters (pull from ContextVariables by name match, backup from State by name match)


        var functionParameters = step.Describe();
        foreach (var param in functionParameters.Parameters)
        {
            if (variables.Get(param.Name, out var value) && !string.IsNullOrEmpty(value))
            {
                functionVariables.Set(param.Name, value);
            }
            else if (this.State.Get(param.Name, out value) && !string.IsNullOrEmpty(value))
            {
                functionVariables.Set(param.Name, value);
            }
        }

        foreach (var item in step.NamedParameters)
        {
            if (item.Value.StartsWith("$", StringComparison.InvariantCultureIgnoreCase))
            {
                var attrValues = item.Value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var attrValueList = new List<string>();
                foreach (var attrValue in attrValues)
                {
                    var attr = attrValue.TrimStart('$');
                    if (variables.Get(attr, out var value) && !string.IsNullOrEmpty(value))
                    {
                        attrValueList.Add(value);
                    }
                    else if (this.State.Get(attr, out value) && !string.IsNullOrEmpty(value))
                    {
                        attrValueList.Add(value);
                    }
                }
                functionVariables.Set(item.Key, string.Concat(attrValueList));
            }
            else
            {
                // if (item.Key != "input" && ) // TODO DO we need this?
                if (!string.IsNullOrEmpty(item.Value))
                {
                    functionVariables.Set(item.Key, item.Value);
                }
                else if (variables.Get(item.Value, out var value) && !string.IsNullOrEmpty(value))
                {
                    functionVariables.Set(item.Key, value);
                }
                else if (this.State.Get(item.Value, out value) && !string.IsNullOrEmpty(value))
                {
                    functionVariables.Set(item.Key, value);
                }
            }
        }




        // OLD Code -- let's do it better now.
        // // step.NamedParameters <string, string> e.g. "input": "EMAIL_TO" -- these always come from state (why not variables override as well?)

        // // step.Describe().Parameters <ParameterInfo> e.g. "input"
        // // Populate as many of the required parameters from the variables, and then the plan State


        // var functionParameters = step.Describe();
        // foreach (var param in functionParameters.Parameters)
        // {
        //     // otherwise get it from the state if present
        //     // todo how was language going through correctly?
        //     if (variables.Get(param.Name, out var value) && !string.IsNullOrEmpty(value))
        //     {
        //         functionVariables.Set(param.Name, value);
        //     }
        // }

        // // NameParameters are the parameters that are passed to the function
        // // These should be pre-populated by the plan, either with a value or a template expression (e.g. $variableName)
        // // The template expression will be replaced with the value of the variable in the variables or the Plan.State
        // // If the variable is not found, the template expression will be replaced with an empty string
        // // Special parameters are:
        // //  - SetContextVariable: The name of a variable in the variables to set with the result of the function
        // //  - AppendToResult: The name of a variable in the variables to append the result of the function to
        // //  - Input: The input to the function. If not specified, the input will be the variables.Input, or the Plan.State.Input, or the Plan.Goal
        // //  - Output: The output of the function. If not specified, the output will be the variables.Output, or the Plan.State.Output, or the Plan.Result
        // // Keys that are not associated with function parameters or special parameters will be ignored
        // foreach (var param in step.NamedParameters)
        // {
        //     if (param.Value.StartsWith("$", StringComparison.InvariantCultureIgnoreCase))
        //     {
        //         // Split the attribute value on the comma or ; character
        //         var attrValues = param.Value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        //         if (attrValues.Length > 0)
        //         {
        //             // If there are multiple values, create a list of the values
        //             var attrValueList = new List<string>();
        //             foreach (var attrValue in attrValues)
        //             {
        //                 var variableName = attrValue[1..];
        //                 if (variables.Get(variableName, out var variableReplacement))
        //                 {
        //                     attrValueList.Add(variableReplacement);
        //                 }
        //                 else if (this.State.Get(attrValue[1..], out variableReplacement))
        //                 {
        //                     attrValueList.Add(variableReplacement);
        //                 }
        //             }

        //             if (attrValueList.Count > 0)
        //             {
        //                 functionVariables.Set(param.Key, string.Concat(attrValueList));
        //             }
        //         }
        //     }
        //     else
        //     {
        //         // TODO
        //         // What to do when step.NameParameters conflicts with the current context?
        //         // Does that only happen with INPUT?
        //         if (param.Key != "INPUT" || !string.IsNullOrEmpty(param.Value))
        //         {
        //             functionVariables.Set(param.Key, param.Value);
        //         }
        //         else
        //         {
        //             // otherwise get it from the state if present
        //             // todo how was language going through correctly?
        //             if (this.State.Get(param.Key, out var value) && !string.IsNullOrEmpty(value))
        //             {
        //                 functionVariables.Set(param.Key, value);
        //             }
        //         }
        //     }
        // }

        return functionVariables;
    }

    private void SetFunction(ISKFunction function)
    {
        this.Function = function;
        this.Name = function.Name;
        this.SkillName = function.SkillName;
        this.Description = function.Description;
        this.IsSemantic = function.IsSemantic;
        this.RequestSettings = function.RequestSettings;
    }

    private ISKFunction? Function { get; set; } = null;
}
