// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Orchestration.Extensions;
using Microsoft.SemanticKernel.Planning.Models;

namespace Microsoft.SemanticKernel.Planning;

public static class KernelPlanningExtensions
{
    // TODO - is the plan the actual Object or is this of type ISKFunction[]?
    // TODO - Experiment with the plan having a cast to ISKFunction[] and see if it works
    private static Task<SKContext> RunAsync(this IKernel kernel, IPlan plan)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public static async Task<IPlan> RunAsync(
        this IKernel kernel,
     string input,
     IPlan plan)
    {
        if (plan is SimplePlan simplePlan)
        {
            SKContext context = kernel.CreateNewContext();

            // Plan.GetNextStep() => (state, step)
            var (nextStep, functionVariables) = simplePlan.PopNextStep(context);
            var variableTargetName = string.Empty; // todo

            var skillName = nextStep.SelectedSkill;
            var functionName = nextStep.SelectedFunction;


            if (context.IsFunctionRegistered(skillName, functionName, out var skillFunction))
            {
                // capture current keys before running function
                var keysToIgnore = functionVariables.Select(x => x.Key).ToList();
                var result = await kernel.RunAsync(functionVariables, skillFunction!);
                // TODO respect ErrorOccurred

                // copy all values for VariableNames in functionVariables not in keysToIgnore to context.Variables
                foreach (var (key, _) in functionVariables)
                {
                    if (!keysToIgnore.Contains(key, StringComparer.InvariantCultureIgnoreCase) && functionVariables.Get(key, out var value))
                    {
                        simplePlan.State.Set(key, value);
                    }
                }

                // TODO -- Get from POp or something
                _ = simplePlan.State.Update(result.Result.Trim());
                if (!string.IsNullOrEmpty(variableTargetName))
                {
                    simplePlan.State.Set(variableTargetName, result.ToString().Trim());
                }

                // if (!string.IsNullOrEmpty(appendToResultName))
                // {
                //     _ = context.Variables.Get(Plan.ResultKey, out var resultsSoFar);
                //     context.Variables.Set(Plan.ResultKey,
                //         string.Join(Environment.NewLine + Environment.NewLine, resultsSoFar, appendToResultName, result.ToString()).Trim());
                // }

                return simplePlan;
            }
        }


        return plan;
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        string input,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        ContextVariables variables,
        IPlan plan)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        ContextVariables variables,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}



