// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

public partial class ForEachSkill
{
    private readonly ISKFunction _toListFunction;

    // For-Each
    public ForEachSkill(ISKFunction toListFunction)
    {
        this._toListFunction = toListFunction;
    }

#pragma warning disable CA1031
    [SKFunction(description: "For-Each")]
    [SKFunctionName("ForEach")]
    [SKFunctionContextParameter(Name = "goalLabel", Description = "Goal labels for plans", DefaultValue = "ForEach")]
    [SKFunctionContextParameter(Name = "stepLabel", Description = "Goal labels for plan steps", DefaultValue = "Step")]
    [SKFunctionContextParameter(Name = "content", Description = "Content to iterate")]
    [SKFunctionContextParameter(Name = "action", Description = "Action to execute on each entry of content e.g. SomeSkill.CallFunction")]
    [SKFunctionContextParameter(Name = "parameters", Description = "Item Parameters to pass to the action")]
    public async Task<SKContext> ForEachAsync(SKContext context)
    {
        context.Variables.Get("goalLabel", out var goalLabel);
        goalLabel ??= "ForEach";
        context.Variables.Get("stepLabel", out var stepLabel);
        stepLabel ??= "Step";
        context.Variables.Get("parameters", out var parameters);
        var forEachContext = context.Variables.Clone();
        if (!context.Variables.Get("action", out var action))
        {
            context.Log.LogError("DoWhile: action not specified");
            return context;
        }

        ISKFunction? functionOrPlan = null;
        try
        {
            functionOrPlan = Plan.FromJson(action, context);
        }
        catch (Exception e)
        {
            context.Log.LogError("DoWhile: action {0} is not a valid plan: {1}", action, e.Message);
        }

        if (functionOrPlan == null)
        {
            if (action.Contains('.', StringComparison.Ordinal))
            {
                var parts = action.Split('.');
                functionOrPlan = context.Skills!.GetFunction(parts[0], parts[1]);
            }
            else
            {
                functionOrPlan = context.Skills!.GetFunction(action);
            }
        }

        if (functionOrPlan == null)
        {
            context.Log.LogError("DoWhile: action {0} not found", action);
            return context;
        }

        var result = await this._toListFunction.InvokeAsync(context);
        // TODO: Error handling
        var list = JsonSerializer.Deserialize<List<string>>(result.Result) ?? new List<string>();

        Plan plan = new(goalLabel);

        foreach (var item in list)
        {
            if (functionOrPlan is Plan planStep)
            {
                // TODO Is this doing too much?
                planStep.State.Update(context.Variables);
                planStep.Description = $"{stepLabel}: {item}";
                if (parameters is not null)
                {
                    planStep.Parameters.Set(parameters, item);
                }

                plan.AddSteps(planStep);
                // reload the plan object
                functionOrPlan = Plan.FromJson(action, context);
            }
            else
            {
                // TODO - anything else to do?
                plan.AddSteps(functionOrPlan);
            }
        }

        // TODO: parameter for the key?
        context.Variables.Set("FOREACH_RESULT", plan.ToJson());

        context.Variables.Update($"Exiting. ForEach {list.Count} items.");
        return context;
    }
#pragma warning restore CA1031
}
