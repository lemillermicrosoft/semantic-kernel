// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Orchestration.Extensions;

namespace Microsoft.SemanticKernel.Planning.Models;

public partial class SimplePlan : BasePlan, IPlanWithSteps
{
    [JsonPropertyName("steps")]
    private List<PlanStep> _steps { get; set; } = new();

    // Today in the Plan, this is the XML String
    public IList<PlanStep> Steps => this._steps;

    public new async Task<IPlan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
    {
        SKContext defaultContext = kernel.CreateNewContext();
        var context = new SKContext(
            variables,
            defaultContext.Memory,
            defaultContext.Skills,
            defaultContext.Log,
            cancellationToken);

        var (nextStep, functionVariables) = this.PopNextStep(context);
        var variableTargetName = string.Empty; // todo

        var skillName = nextStep.SelectedSkill;
        var functionName = nextStep.SelectedFunction;

        if (context.IsFunctionRegistered(skillName, functionName, out var skillFunction))
        {
            // capture current keys before running function
            var keysToIgnore = functionVariables.Select(x => x.Key).ToList();
            var result = await kernel.RunAsync(functionVariables, cancellationToken, skillFunction!);
            // TODO respect ErrorOccurred

            // copy all values for VariableNames in functionVariables not in keysToIgnore to context.Variables
            foreach (var (key, _) in functionVariables)
            {
                if (!keysToIgnore.Contains(key, StringComparer.InvariantCultureIgnoreCase) && functionVariables.Get(key, out var value))
                {
                    this.State.Set(key, value);
                }
            }

            // TODO -- Get from POp or something
            _ = this.State.Update(result.Result.Trim());
            if (!string.IsNullOrEmpty(variableTargetName))
            {
                this.State.Set(variableTargetName, result.ToString().Trim());
            }

            // if (!string.IsNullOrEmpty(appendToResultName))
            // {
            //     _ = context.Variables.Get(Plan.ResultKey, out var resultsSoFar);
            //     context.Variables.Set(Plan.ResultKey,
            //         string.Join(Environment.NewLine + Environment.NewLine, resultsSoFar, appendToResultName, result.ToString()).Trim());
            // }

            return this;
        }
        else
        {
            // TODO
            throw new InvalidOperationException($"Function {skillName}.{functionName} is not registered");
        }
    }

    private (PlanStep step, ContextVariables context) PopNextStep(SKContext skContext)
    {
        var step = this._steps[0];

        // var planInput = string.IsNullOrEmpty(this.State.Input) ? this.State.ToString() : this.State.Input;
        // string functionInput = string.IsNullOrEmpty(planInput) ? this.Goal : planInput;
        // var functionVariables = new ContextVariables(functionInput);

        // Actually, priority should be the skContext Input, then the Plan Input, then the Goal
        var planInput = string.IsNullOrEmpty(skContext.Variables.Input) ? this.State.Input : skContext.Variables.Input;
        var functionInput = string.IsNullOrEmpty(planInput) ? this.Goal : planInput;
        var functionVariables = new ContextVariables(functionInput);


        var stepAndTextResults = new StringBuilder();

        var functionName = step.SelectedFunction;
        var skillName = step.SelectedSkill;

        // TODO REmove this check and just prepare the context
        if (!string.IsNullOrEmpty(functionName) && skContext.IsFunctionRegistered(skillName, functionName, out var skillFunction))
        {
            Verify.NotNull(functionName, nameof(functionName));
            Verify.NotNull(skillFunction, nameof(skillFunction));
            skContext.Log.LogTrace("Processing step {0}.{1}", skillName, functionName);

            var variableTargetName = string.Empty;
            var appendToResultName = string.Empty;

            foreach (var param in step.NamedParameters)
            {
                skContext.Log.LogTrace("processing named parameter {0}", param.Key);
                if (param.Value.StartsWith("$", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Split the attribute value on the comma or ; character
                    var attrValues = param.Value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (attrValues.Length > 0)
                    {
                        // If there are multiple values, create a list of the values
                        var attrValueList = new List<string>();
                        foreach (var attrValue in attrValues)
                        {
                            if (this.State.Get(attrValue[1..], out var variableReplacement))
                            {
                                attrValueList.Add(variableReplacement);
                            }
                        }

                        if (attrValueList.Count > 0)
                        {
                            functionVariables.Set(param.Key, string.Concat(attrValueList));
                        }
                    }
                }
                else if (param.Key.Equals(SetContextVariableTag, StringComparison.OrdinalIgnoreCase))
                {
                    variableTargetName = param.Value;
                }
                else if (param.Key.Equals(AppendToResultTag, StringComparison.OrdinalIgnoreCase))
                {
                    appendToResultName = param.Value;
                }
                else
                {
                    // TODO
                    // What to do when step.NameParameters conflicts with the current context?
                    // Does that only happen with INPUT?
                    if (param.Key != "INPUT")
                    {
                        functionVariables.Set(param.Key, param.Value);
                    }
                }
            }
        }

        this._steps.RemoveAt(0);
        return (step, functionVariables); // TODO Yeah, how does this.State get updated?
    }

    /// <summary>
    /// The attribute tag used in the plan xml for setting the context variable name to set the output of a function to.
    /// </summary>
    internal const string SetContextVariableTag = "setContextVariable";

    /// <summary>
    /// The attribute tag used in the plan xml for appending the output of a function to the final result for a plan.
    /// </summary>
    internal const string AppendToResultTag = "appendToResult";
}
