// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
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

    public Task<IPlan> CreatePlanAsync(string goal)
    {
        this._context.Variables.Update(goal);

        var plan = new BasePlan()
        {
            Goal = goal,
        };

        return Task.FromResult<IPlan>(plan);
    }

    protected PlannerSkillConfig Config { get; }

    private readonly SKContext _context;
    private readonly IKernel _kernel;
}
