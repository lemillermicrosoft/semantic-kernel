// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.Models;
using static Microsoft.SemanticKernel.CoreSkills.PlannerSkill;

namespace Microsoft.SemanticKernel.Planning.Planners;


public class SimplePlanner : IPlanner
{
    public SimplePlanner(IKernel kernel, int maxTokens) : this(kernel, maxTokens, null)
    {
    }

    protected SimplePlanner(IKernel kernel, int maxTokens, PlannerSkillConfig? config)
    {
        this.Config = config ?? new();

        this._kernel = kernel;
        this._context = kernel.CreateNewContext();
    }

    public async Task<IPlan> CreatePlanAsync(string goal)
    {
        string relevantFunctionsManual = await this._context.GetFunctionsManualAsync(goal, this.Config);
        this._context.Variables.Set("available_functions", relevantFunctionsManual);

        this._context.Variables.Update(goal);

        var plan = new BasePlan()
        {
            Goal = goal,
        };

        return plan;
    }

    Task<IPlan> IPlanner.CreatePlanAsync(string goal)
    {
        throw new System.NotImplementedException();
    }

    protected PlannerSkillConfig Config { get; }

    private readonly SKContext _context;
    private readonly IKernel _kernel;
}
