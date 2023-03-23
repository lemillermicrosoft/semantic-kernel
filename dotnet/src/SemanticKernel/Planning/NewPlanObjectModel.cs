// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Orchestration.Extensions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Planning;

public interface IPlan
{
    // id

    string Goal { get; }

    // Today, the result of calling Create|Execute plan is SKContext with ContextVariables that contain both the state and the plan itself.
    // In future, methods or extensions for RunPlan will instead return the plan, with the context place inside of it. Need to understand the why better.
    ContextVariables State { get; } // TODO WorkingMemory instead?

    string ToPlanString();

    // this will be a function that calls the next step, and updates context with the result and new plan
    ISKFunction NextStep();
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

    public virtual ISKFunction NextStep()
    {
        throw new System.NotImplementedException();
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

    // Today in the Plan, this is the XML String
    public IList<PlanStep> Steps => steps;

    public override ISKFunction NextStep(SKContext context)
    {
        return ExecutePlanAsync(context)
    }

    [SKFunction("Execute a plan that uses registered functions to accomplish a goal.")]
    [SKFunctionName("ExecutePlan")]
    public async Task<SKContext> ExecutePlanAsync(SKContext context)
    {
        var planToExecute = context.Variables.ToPlan();
        try
        {
            var executeResultContext = await this._functionFlowRunner.ExecuteXmlPlanAsync(context, planToExecute.PlanString);
            _ = executeResultContext.Variables.Get(Plan.PlanKey, out var planProgress);
            _ = executeResultContext.Variables.Get(Plan.ResultKey, out var results);

            var isComplete = planProgress.Contains($"<{FunctionFlowRunner.SolutionTag}>", StringComparison.InvariantCultureIgnoreCase) &&
                             !planProgress.Contains($"<{FunctionFlowRunner.FunctionTag}", StringComparison.InvariantCultureIgnoreCase);
            var isSuccessful = !executeResultContext.ErrorOccurred &&
                               planProgress.Contains($"<{FunctionFlowRunner.SolutionTag}>", StringComparison.InvariantCultureIgnoreCase);

            if (string.IsNullOrEmpty(results) && isComplete && isSuccessful)
            {
                results = executeResultContext.Variables.ToString();
            }
            else if (executeResultContext.ErrorOccurred)
            {
                results = executeResultContext.LastErrorDescription;
            }

            _ = context.Variables.UpdateWithPlanEntry(new Plan
            {
                Id = planToExecute.Id,
                Goal = planToExecute.Goal,
                PlanString = planProgress,
                IsComplete = isComplete,
                IsSuccessful = isSuccessful,
                Result = results,
            });

            return context;
        }
        catch (PlanningException e)
        {
            switch (e.ErrorCode)
            {
                case PlanningException.ErrorCodes.InvalidPlan:
                    context.Log.LogWarning("[InvalidPlan] Error executing plan: {0} ({1})", e.Message, e.GetType().Name);
                    _ = context.Variables.UpdateWithPlanEntry(new Plan
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Goal = planToExecute.Goal,
                        PlanString = planToExecute.PlanString,
                        IsComplete = true, // Plan was invalid, mark complete so it's not attempted further.
                        IsSuccessful = false,
                        Result = e.Message,
                    });

                    return context;
                case PlanningException.ErrorCodes.UnknownError:
                case PlanningException.ErrorCodes.InvalidConfiguration:
                    context.Log.LogWarning("[UnknownError] Error executing plan: {0} ({1})", e.Message, e.GetType().Name);
                    break;
                default:
                    throw;
            }
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            context.Log.LogWarning("Error executing plan: {0} ({1})", e.Message, e.GetType().Name);
            _ = context.Variables.UpdateWithPlanEntry(new Plan
            {
                Id = Guid.NewGuid().ToString("N"),
                Goal = planToExecute.Goal,
                PlanString = planToExecute.PlanString,
                IsComplete = false,
                IsSuccessful = false,
                Result = e.Message,
            });

            return context;
        }

        return context;
    }

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
    public ContextVariables NamedParameters { get; set; } = new();
    // TODO Parameter? Key Value Pair of a Parameter type? Is this connected to FunctionView or a subset of it?
    // Key is FunctionView Parameter and value is the value -- what about handling output from other functions early in the plan?
    // Pointer reference is OKAY in that case and substitution is done later at execution?

    [JsonPropertyName("manifests")]
    public FunctionView Manifests { get; set; } = new();
    // TODO probably not FunctionView -- what will this be used for, who needs to define it, will this include remote execution details?
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
