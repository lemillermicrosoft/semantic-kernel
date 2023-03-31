// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning;

public class SimplePlan : BasePlan
{
    public new async Task<IPlan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
    {
        var context = kernel.CreateNewContext();
        var nextStep = this.PopNextStep();
        var functionVariables = this.GetNextStepVariables(variables, nextStep);

        var skillName = nextStep.SelectedSkill;
        var functionName = nextStep.SelectedFunction;

        if (context.IsFunctionRegistered(skillName, functionName, out var skillFunction))
        {
            // If a function is registered, we will execute it and remove it from the list
            // The functionVariables will be passed to the functions.
            var keysToIgnore = functionVariables.Select(x => x.Key).ToList();
            var result = await kernel.RunAsync(functionVariables, cancellationToken, skillFunction!);

            if (result.ErrorOccurred)
            {
                throw new InvalidOperationException($"Function {skillName}.{functionName} failed: {result.LastErrorDescription}", result.LastException);
            }

            foreach (var (key, _) in functionVariables)
            {
                if (!keysToIgnore.Contains(key, StringComparer.InvariantCultureIgnoreCase) && functionVariables.Get(key, out var value))
                {
                    this.State.Set(key, value);
                }
            }

            if (!string.IsNullOrEmpty(nextStep.OutputKey))
            {
                _ = this.State.Update(result.Result.Trim());
            }
            else
            {
                this.State.Set(nextStep.OutputKey, result.Result.Trim());
            }

            _ = this.State.Update(result.Result.Trim());
            if (!string.IsNullOrEmpty(nextStep.OutputKey))
            {
                this.State.Set(nextStep.OutputKey, result.Result.Trim());
            }

            if (!string.IsNullOrEmpty(nextStep.ResultKey))
            {
                _ = this.State.Get(SkillPlan.ResultKey, out var resultsSoFar);
                this.State.Set(SkillPlan.ResultKey,
                    string.Join(Environment.NewLine + Environment.NewLine, resultsSoFar, result.Result.Trim()));
            }
            return this;
        }
        else
        {
            throw new InvalidOperationException($"Function {skillName}.{functionName} is not registered");
        }
    }

    private ContextVariables GetNextStepVariables(ContextVariables variables, PlanStep step)
    {
        // Initialize function-scoped ContextVariables
        // Default input should be the Input from the SKContext, or the Input from the Plan.State, or the Plan.Goal
        var planInput = string.IsNullOrEmpty(variables.Input) ? this.State.Input : variables.Input;
        var functionInput = string.IsNullOrEmpty(planInput) ? this.Goal : planInput;
        var functionVariables = new ContextVariables(functionInput);

        // NameParameters are the parameters that are passed to the function
        // These should be pre-populated by the plan, either with a value or a template expression (e.g. $variableName)
        // The template expression will be replaced with the value of the variable in the variables or the Plan.State
        // If the variable is not found, the template expression will be replaced with an empty string
        // Special parameters are:
        //  - SetContextVariable: The name of a variable in the variables to set with the result of the function
        //  - AppendToResult: The name of a variable in the variables to append the result of the function to
        //  - Input: The input to the function. If not specified, the input will be the variables.Input, or the Plan.State.Input, or the Plan.Goal
        //  - Output: The output of the function. If not specified, the output will be the variables.Output, or the Plan.State.Output, or the Plan.Result
        // Keys that are not associated with function parameters or special parameters will be ignored
        foreach (var param in step.NamedParameters)
        {
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
                        var variableName = attrValue[1..];
                        if (variables.Get(variableName, out var variableReplacement))
                        {
                            attrValueList.Add(variableReplacement);
                        }
                        else if (this.State.Get(attrValue[1..], out variableReplacement))
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

        return functionVariables;
    }

    private PlanStep PopNextStep()
    {
        var step = this.Steps;
        var parent = step;
        while (step.Children.Count > 0)
        {
            step = step.Children[0];
        }

        parent.Children.RemoveAt(0); // TODO Does this do what I want? has this.Steps changed?

        return step;
    }
}
