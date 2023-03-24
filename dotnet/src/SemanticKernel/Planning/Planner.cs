// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using static Microsoft.SemanticKernel.CoreSkills.PlannerSkill;

namespace Microsoft.SemanticKernel.Planning;


public class Planner
{
    public enum Mode
    {
        Simple,
        Complex
    }

    public Planner(IKernel kernel, Mode mode = Mode.Simple, int maxTokens = 1024)
    {
        this._kernel = kernel;
        this._mode = mode; // needed?
        this._maxTokens = maxTokens;
        this._planner = this.GetPlannerForMode(this._mode);

        // TODO do I need a handle to this? Does the planner itself?
        _ = kernel.ImportSkill(this._planner, "PlannerObjectSkill");
    }


    // CreatePlan(string goal)
    public Task<IPlan> CreatePlanAsync(string goal)
    {
        return this._planner.CreatePlanAsync(goal);
    }

    // public Task<IPlan> ExecutePlan(IPlan plan)
    // {

    // }

    private IPlanner GetPlannerForMode(Mode mode)
    {
        return mode switch
        {
            Mode.Simple => new SimplePlanner(this._kernel, this._maxTokens),
            Mode.Complex => new ComplexPlanner(this._kernel, this._maxTokens),
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


public class SimplePlanner : IPlanner
{
    public SimplePlanner(IKernel kernel, int maxTokens) : this(kernel, maxTokens, null)
    {
    }

    protected SimplePlanner(IKernel kernel, int maxTokens, PlannerSkillConfig? config)
    {
        this._functionFlowRunner = new(kernel);

        this._functionFlowFunction = kernel.CreateSemanticFunction(
            promptTemplate: SemanticFunctionConstants.FunctionFlowFunctionDefinition,
            skillName: RestrictedSkillName,
            description: "Given a request or command or goal generate a step by step plan to " +
                        "fulfill the request using functions. This ability is also known as decision making and function flow",
            maxTokens: maxTokens,
            temperature: 0.0,
            stopSequences: new[] { "<!--" });

        this.Config = config ?? new()
        {
            RelevancyThreshold = 0,
            MaxRelevantFunctions = int.MaxValue
        };

        this._kernel = kernel;
        this._context = kernel.CreateNewContext();
    }

    public async Task<IPlan> CreatePlanAsync(string goal)
    {
        string relevantFunctionsManual = await this._context.GetFunctionsManualAsync(goal, this.Config);
        this._context.Variables.Set("available_functions", relevantFunctionsManual);
        // TODO - consider adding the relevancy score for functions added to manual

        // TODO - update _functionFlowFunction to return a serialized IPlan
        var result = await this._functionFlowFunction.InvokeAsync(this._context);



        return SimplePlan.FromString(result.Result);

        // string fullPlan = $"<{FunctionFlowRunner.GoalTag}>\n{goal}\n</{FunctionFlowRunner.GoalTag}>\n{plan.ToString().Trim()}";
        // _ = this._context.Variables.UpdateWithPlanEntry(new Plan
        // {
        //     Id = Guid.NewGuid().ToString("N"),
        //     Goal = goal,
        //     PlanString = fullPlan,
        // });

        // return this._context;
    }

    protected PlannerSkillConfig Config { get; }

    private readonly SKContext _context;
    private readonly IKernel _kernel;

    /// <summary>
    /// the function flow runner, which executes plans that leverage functions
    /// </summary>
    private readonly FunctionFlowRunner _functionFlowRunner;

    /// <summary>
    /// the function flow semantic function, which takes a goal and creates an xml plan that can be executed
    /// </summary>
    private readonly ISKFunction _functionFlowFunction;

    /// <summary>
    /// The name to use when creating semantic functions that are restricted from the PlannerSkill plans
    /// </summary>
    private const string RestrictedSkillName = "PlannerSkill_Excluded";
}

public class ComplexPlanner : SimplePlanner
{
    public ComplexPlanner(IKernel kernel, int maxTokens) : base(kernel, maxTokens, new())
    {
    }
}
