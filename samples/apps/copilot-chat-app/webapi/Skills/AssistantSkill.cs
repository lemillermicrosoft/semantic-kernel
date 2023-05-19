// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

// Depends on: ForEachSkill
// Context requires: StudySkill
public class AssistantSkill
{
    public IKernel Kernel { get; private set; }

    public AssistantSkill(IKernel kernel)
    {
        this.Kernel = kernel;

        string folder = FunctionLoadingExtensions.SampleSkillsPath();

        var t = this.Kernel.ImportSemanticSkillFromDirectory(folder, "JsonSkill");
        this.Kernel.ImportSkill(new JsonSkill(t["SemanticSelect"]), "JsonSkill");

        this.Kernel.ImportSemanticSkillFromDirectory(Path.GetFullPath(Path.Combine(Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location), "..")), "SummarizeSkill", "WriterSkill");

    }

    // ProblemSolve
    [SKFunctionName("ProblemSolve")]
    [SKFunction("Given a goal or user ask, create a sequential plan to achieve the goal or solve the problem. Contains ability to interact with plugins like Jira and Microsot Graph as well as Summarize and Writer skills. Keywords for plan: Assistant, AssistantSkill, ProblemSolve, Solve a problem, Do this for me, Help me")]
    [SKFunctionContextParameter(Name = "input", Description = "Goal to achieve or problem to solve")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat")]
    [SKFunctionContextParameter(Name = "userId", Description = "ID of the user who owns the documents")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens")]
    public async Task<SKContext> ProblemSolveAsync(SKContext context)
    {
        context.Variables.Get("chatId", out var chatId);
        context.Variables.Get("userId", out var userId);
        context.Variables.Get("tokenLimit", out var tokenLimit);
        context.Variables.Get("contextTokenLimit", out var contextTokenLimit);
        context.Variables.Get("input", out var input);

        Console.WriteLine($"*understands* I see you want to create a plan to '{input}'");

        var planner = new SequentialPlanner(this.Kernel);
#pragma warning disable CA1031
        try
        {
            var plan = await planner.CreatePlanAsync(input);
            context.Variables.Set("action", plan.ToJson());
            Console.WriteLine($"{plan.ToJson(true)}");
            context.Variables.Update(plan.ToJson(true));

            // TODO -- Don't couple with LearningSkill -- follow teresa pattern
            await context.Memory.SaveInformationAsync(
                collection: $"{chatId}-LearningSkill.LessonPlans",
                text: plan.ToJson(true),
                id: Guid.NewGuid().ToString(),
                description: $"Plan for '{input}'",
                additionalMetadata: plan.ToJson());
        }
        catch (Exception e)
        {
            Console.WriteLine($"*understands* I couldn't create a plan for '{input}'");
            Console.WriteLine(e);
        }
        return context;
#pragma warning restore CA1031
    }
}
