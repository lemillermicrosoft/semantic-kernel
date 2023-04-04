// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Planning.Planners;

public class SimplePlanner : IPlanner
{
    public SimplePlanner()
    {
    }

    public Task<Plan> CreatePlanAsync(string goal)
    {
        var plan = new Plan()
        {
            Description = goal,
        };

        return Task.FromResult<Plan>(plan);
    }
}
