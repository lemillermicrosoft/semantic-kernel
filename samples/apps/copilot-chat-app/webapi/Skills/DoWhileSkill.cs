// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

public class DoWhileSkill
{
    // Planner semantic function
    private readonly ISKFunction _isTrueFunction;

    // Do-While
    public DoWhileSkill(ISKFunction isTrueFunction)
    {
        this._isTrueFunction = isTrueFunction;
    }

#pragma warning disable CA1031
    [SKFunction(description: "Do-While")]
    [SKFunctionName("DoWhile")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    [SKFunctionContextParameter(Name = "action", Description = "Action to execute e.g. SomeSkill.CallFunction")]
    public async Task<SKContext> DoWhileAsync(SKContext context)
    {
        var doWhileContext = context.Variables.Clone();
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

        bool isTrue;
        do
        {
            var contextResponse = await functionOrPlan.InvokeAsync(context); // or the action? or maybe other actions after certain conditions?

            if (functionOrPlan is Plan)
            {
                // Reload the plan so it can be executed again
                functionOrPlan = Plan.FromJson(action, context);
            }

            // doWhileContext.Set("context", context.Variables.Input); // TODO

            isTrue = await this.IsTrueAsync(context);
        } while (isTrue);

        context.Variables.Update($"Exiting. Condition '{context.Variables["condition"]}' is false");
        return context;
    }
#pragma warning restore CA1031

    [SKFunction(description: "Is True")]
    [SKFunctionName("IsTrue")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    public async Task<bool> IsTrueAsync(SKContext context)
    {
        var state = JsonSerializer.Serialize(context.Variables); // todo what is this doing?
        var result = await this._isTrueFunction.InvokeAsync(context);
        if (bool.TryParse(result.Result.ToString(), out var isTrue) || result.Result.ToString().Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return isTrue;
        }
        else
        {
            context.Log.LogError("IsTrue: condition '{0}' not found", result);
            return false;
        }
    }
}
