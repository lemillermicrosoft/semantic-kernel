// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using static Microsoft.SemanticKernel.CoreSkills.PlannerSkill;

namespace Microsoft.SemanticKernel.Planning.Planners;

public class Planner
{
    public enum Mode
    {
        Simple,
        FunctionFlow,
        GoalRelevant,
    }

    public Planner(IKernel kernel, Mode mode = Mode.FunctionFlow, int maxTokens = 1024, PlannerSkillConfig? config = null)
    {
        this._kernel = kernel;
        this._mode = mode;
        this._maxTokens = maxTokens;
        this._config = config;
        this._planner = this.GetPlannerForMode(this._mode);
    }

    public Task<Plan> CreatePlanAsync(string goal)
    {
        return this._planner.CreatePlanAsync(goal);
    }

    private IPlanner GetPlannerForMode(Mode mode)
    {
        return mode switch
        {
            Mode.Simple => new SimplePlanner(this._kernel, this._maxTokens),
            Mode.GoalRelevant => this._config is null ? new GoalRelevantPlanner(this._kernel, this._maxTokens) : new GoalRelevantPlanner(this._kernel, this._maxTokens, this._config),
            Mode.FunctionFlow => new FunctionFlowPlanner(this._kernel, this._maxTokens),
            _ => throw new NotImplementedException(),
        };
    }

    private readonly IPlanner _planner;
    private readonly IKernel _kernel;
    private readonly Mode _mode;
    private readonly int _maxTokens;
    private readonly PlannerSkillConfig? _config;
}

public interface IPlanner
{
    Task<Plan> CreatePlanAsync(string goal);
}
