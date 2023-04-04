﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning.Planners;

public class FunctionFlowPlanner : IPlanner
{
    public FunctionFlowPlanner(IKernel kernel, PlannerConfig? config)
    {
        this.Config = config ?? new();

        this._functionFlowFunction = kernel.CreateSemanticFunction(
            promptTemplate: SemanticFunctionConstants.FunctionFlowFunctionDefinition,
            skillName: RestrictedSkillName,
            description: "Given a request or command or goal generate a step by step plan to " +
                         "fulfill the request using functions. This ability is also known as decision making and function flow",
            maxTokens: this.Config.MaxTokens,
            temperature: 0.0,
            stopSequences: new[] { "<!--" });

        this._context = kernel.CreateNewContext();
    }

    public async Task<Plan> CreatePlanAsync(string goal)
    {
        string relevantFunctionsManual = await this._context.GetFunctionsManualAsync(goal, this.Config);
        this._context.Variables.Set("available_functions", relevantFunctionsManual);

        this._context.Variables.Update(goal);

        var planResult = await this._functionFlowFunction.InvokeAsync(this._context);

        // TODO Do we need to do this actually?
        string fullPlan = $"<{FunctionFlowParser.GoalTag}>\n{goal}\n</{FunctionFlowParser.GoalTag}>\n{planResult.Result.Trim()}";

        var plan = fullPlan.ToPlanFromXml(this._context);

        return plan;
    }

    protected PlannerConfig Config { get; }

    private readonly SKContext _context;

    /// <summary>
    /// the function flow semantic function, which takes a goal and creates an xml plan that can be executed
    /// </summary>
    private readonly ISKFunction _functionFlowFunction;

    /// <summary>
    /// The name to use when creating semantic functions that are restricted from the PlannerSkill plans
    /// </summary>
    private const string RestrictedSkillName = "PlannerSkill_Excluded"; // todo we've got copies of this now
}

public class GoalRelevantPlanner : FunctionFlowPlanner
{
    public GoalRelevantPlanner(IKernel kernel) : base(kernel, new() { RelevancyThreshold = 0.78 })
    {
    }

    public GoalRelevantPlanner(IKernel kernel, PlannerConfig? config) : base(kernel, config)
    {
    }
}
