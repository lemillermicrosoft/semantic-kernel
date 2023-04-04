// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Planning.Planners;

public class Planner
{
    public enum Mode
    {
        Simple,
        FunctionFlow,
        GoalRelevant,
    }

    public Planner(IKernel kernel, Mode mode = Mode.FunctionFlow, PlannerConfig? config = null)
    {
        this._kernel = kernel;
        this._mode = mode;
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
            Mode.Simple => new SimplePlanner(),
            Mode.GoalRelevant => new GoalRelevantPlanner(this._kernel, this._config),
            Mode.FunctionFlow => new FunctionFlowPlanner(this._kernel, this._config),
            _ => throw new NotImplementedException(),
        };
    }

    private readonly IPlanner _planner;
    private readonly IKernel _kernel;
    private readonly Mode _mode;
    private readonly PlannerConfig? _config;
}

public interface IPlanner
{
    Task<Plan> CreatePlanAsync(string goal);
}
