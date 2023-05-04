// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using RepoUtils;

namespace Skills;

// Create a skill that abstracts the "Given a plan and condition, run it"
public class AgentSkill
{
    private readonly IKernel _agentSkillKernel;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;

    public AgentSkill(IKernel kernel)
    {
        // Create a kernel
        this._agentSkillKernel = kernel;

        string folder = RepoFiles.SampleSkillsPath();
        this._semanticSkills = (IDictionary<string, ISKFunction>)this._agentSkillKernel.ImportSemanticSkillFromDirectory(folder, "DoWhileSkill");
        this._doWhileSkill = this._agentSkillKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");
        this._agentSkillKernel.ImportSkill(this, "AgentSkill");
    }

    // RunPlan
    [SKFunction(description: "Run a plan with a condition")]
    [SKFunctionName("RunPlan")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    [SKFunctionContextParameter(Name = "plan", Description = "Plan to execute")]
    public async Task<SKContext> RunPlanAsync(SKContext context)
    {
        var runPlanContext = context.Variables.Clone();
        if (!context.Variables.Get("plan", out var plan))
        {
            context.Log.LogError("RunPlan: plan not specified");
            return context;
        }

        ISKFunction? functionOrPlan = null;
        try
        {
            functionOrPlan = Plan.FromJson(plan, context);
        }
        catch (Exception e)
        {
            context.Log.LogError("RunPlan: plan {0} is not a valid plan: {1}", plan, e.Message);
        }

        if (functionOrPlan == null)
        {
            context.Log.LogError("RunPlan: plan {0} not found", plan);
            return context;
        }

        runPlanContext.Set("action", plan);

        return await this._agentSkillKernel.RunAsync(runPlanContext, this._doWhileSkill["DoWhile"]); // TODO WRONG ONE need native
    }
}
