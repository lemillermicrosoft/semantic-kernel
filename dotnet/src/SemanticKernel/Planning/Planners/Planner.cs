// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Planning.Models;

namespace Microsoft.SemanticKernel.Planning.Planners;

public class Planner
{
    public enum Mode
    {
        Simple,
        FunctionFlow,
        GoalRelevant,

    }

    public Planner(IKernel kernel, Mode mode = Mode.Simple, int maxTokens = 1024)
    {
        this._kernel = kernel;
        this._mode = mode; // needed?
        this._maxTokens = maxTokens;
        this._planner = this.GetPlannerForMode(this._mode);
    }

    public Task<IPlan> CreatePlanAsync(string goal)
    {
        return this._planner.CreatePlanAsync(goal);
    }

    private IPlanner GetPlannerForMode(Mode mode)
    {
        return mode switch
        {
            Mode.Simple => new FunctionFlowPlanner(this._kernel, this._maxTokens),
            Mode.GoalRelevant => new GoalRelevantPlanner(this._kernel, this._maxTokens),
            Mode.FunctionFlow => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }

    private readonly IPlanner _planner;

    private readonly IKernel _kernel;
    private readonly Mode _mode;
    private readonly int _maxTokens;
}


public interface IPlanner
{
    Task<IPlan> CreatePlanAsync(string goal);
}
