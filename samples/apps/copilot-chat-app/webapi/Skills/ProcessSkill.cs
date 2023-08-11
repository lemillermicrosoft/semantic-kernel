// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

// Depends on: ForEachSkill
// Context requires: StudySkill
public class ProcessSkill
{
    private readonly IKernel _processSkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _processSkill;

    private readonly IDictionary<string, ISKFunction> _forEachSkill;

    #region create a kernel

    public ProcessSkill(IKernel kernel)
    {
        // Create a kernel
        this._processSkillKernel = kernel;

        #endregion

        string folder = FunctionLoadingExtensions.SampleSkillsPath();
        this._semanticSkills = this._processSkillKernel.ImportSemanticSkillFromDirectory(folder,
            "ForEachSkill");
        this._processSkill = this._processSkillKernel.ImportSkill(this, "ProcessSkill");

        this._forEachSkill = this._processSkillKernel.ImportSkill(new ForEachSkill(this._processSkill["ToList"]), "ForEachSkill");

    }

    [SKFunctionName("ToList")]
    [SKFunction("Get a list of items")]
    public Task<SKContext> ToList(SKContext context)
    {
        var loanCriteria = new string[]{
            "Check the borrower's credit score to ensure it meets the minimum requirement set by the lender.",
            "Confirm the borrower's employment status, including job stability and income level.",
            "Calculate the borrower's debt-to-income ratio to ensure it falls within the acceptable range set by the lender.",
            "Verify the purpose of the loan and ensure it aligns with the lender's acceptable loan purposes.",
            "Ensure the requested loan amount is within the lender's allowable range.",
            "Assess the borrower's capacity to repay the loan, considering their monthly income, expenses, and other financial commitments.",
            "If the loan is secured, confirm the borrower has sufficient collateral to secure the loan and evaluate its value.",
            "If a co-signer is required, confirm the co-signer's creditworthiness and willingness to assume responsibility for the loan.",
            "Verify that the borrower has provided all necessary documentation, such as proof of income, bank statements, and identification.",
            "Confirm that the borrower understands and agrees to the loan terms, including interest rate, repayment schedule, and any fees or penalties."
        };

        context.Variables.Update(JsonSerializer.Serialize(loanCriteria));
        return Task.FromResult(context);
    }

    // CreateLesson
    [SKFunctionName("GetProcess")]
    [SKFunctionContextParameter(Name = "processName", Description = "Name of the process", DefaultValue = "Personal loan creation")]
    [SKFunctionContextParameter(Name = "processDescription", Description = "Description of the process", DefaultValue = "Create a personal loan")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat")]
    [SKFunctionContextParameter(Name = "userId", Description = "ID of the user who owns the documents")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens")]
    [SKFunction("Get a process")]
    public async Task<SKContext> GetProcessAsync(SKContext context)
    {
        context.Variables.Get("processName", out var processName);
        context.Variables.Get("processDescription", out var processDescription);

        processName = string.IsNullOrEmpty(processName) ? "Personal loan creation" : processName;
        processDescription = string.IsNullOrEmpty(processDescription) ? "Create a personal loan" : processDescription;

        context.Variables.Get("chatId", out var chatId);
        context.Variables.Get("userId", out var userId);
        context.Variables.Get("tokenLimit", out var tokenLimit);
        context.Variables.Get("contextTokenLimit", out var contextTokenLimit);

        Console.WriteLine($"*understands* I see you want to get a process plan for {processName}");

        var plan = new Plan(processDescription)
        {
            Name = $"{processName} Buddy"
        };

        // TODO Future
        if (context.Skills?.TryGetFunction("BankAgentPlugin", "GetProcessCriteria", out var GetLessonTopics) == true)
        {
            /*
            context.Variables.Update(processName);

            var processContext = processDescription;
            if (context.Skills is not null && context.Skills.TryGetFunction("DocumentMemorySkill", "QueryDocuments", out var queryDocuments))
            {
                var documentContext = new ContextVariables(string.Join(" ", processName, processDescription));
                documentContext.Set("userId", userId);
                documentContext.Set("tokenLimit", tokenLimit);
                documentContext.Set("contextTokenLimit", contextTokenLimit);
                var queryDocumentsContext = new SKContext(documentContext, context.Memory, context.Skills, context.Log, context.CancellationToken);

                var queryDocumentsResult = await queryDocuments.InvokeAsync(queryDocumentsContext);
                processContext = queryDocumentsResult.Result.ToString();
                if (string.IsNullOrEmpty(processContext))
                {
                    Console.WriteLine($"*understands* I didn't find any documents about {processName}");
                }
                else
                {
                    Console.WriteLine($"*understands* I found {processContext}");
                }
            }
            else
            {
                Console.WriteLine("DocumentMemorySkill not found");
            }

            // TODO: Make this better
            context.Variables.Set("context", processContext); // {DocumentMemorySkill.QueryDocuments $INPUT}
            var course = context.Variables.Input;
            context = await GetLessonTopics.InvokeAsync(context);

            // TODO: maybe use the output of GetLessonTopics too
            var studyPlan = new Plan(processDescription);
            studyPlan.State.Set("course", course);
            if (context.Skills is not null &&
                context.Skills.TryGetFunction("StudySkill", "StudySession", out var gatherSession))
            {
                // TODO either make gatherSession plan and then set its outputs or just make that the studyPlan
                var p = new Plan(gatherSession);
                p.Outputs.Add("LESSON_STATE");
                studyPlan.AddSteps(p);
            }

            var forEachContext = new ContextVariables(context.Result.ToString());

            forEachContext.Set("goalLabel", "Complete Lessons");
            forEachContext.Set("stepLabel", "Complete Lesson");
            forEachContext.Set("action", studyPlan.ToJson());
            forEachContext.Set("content", context.Result.ToString()); // todo advanced condition like amount of time, etc.

            // set the name of the Parameters key to give each `item` in the foreach a name
            forEachContext.Set("Parameters", "topic"); // This is skill specific
            var result = await this._processSkillKernel.RunAsync(forEachContext, this._forEachSkill["ForEach"]);
            if (result.Variables.Get("FOREACH_RESULT", out var forEachResultPlan))
            {
                plan = Plan.FromJson(forEachResultPlan.ToString(), context);
                plan.Name = $"{processName} Buddy";
                plan.Description = processDescription;
            }
            */
        }
        else // hardcoded
        {
            var studyPlan = new Plan(processDescription);
            // var course = context.Variables.Input;
            studyPlan.State.Set("process", processName);
            if (context.Skills is not null &&
                context.Skills.TryGetFunction("BankAgentPlugin", "GatherProcessRequirements", out var gatherSession))
            {
                // TODO either make gatherSession plan and then set its outputs or just make that the studyPlan
                var p = new Plan(gatherSession);
                p.Outputs.Add("LESSON_STATE");
                studyPlan.AddSteps(p);
            }

            var forEachContext = new ContextVariables(context.Result.ToString());

            // forEachContext.Set("goalLabel", "Complete Lessons");
            // forEachContext.Set("stepLabel", "Complete Lesson");
            forEachContext.Set("goalLabel", string.Empty);
            forEachContext.Set("stepLabel", string.Empty);
            forEachContext.Set("action", studyPlan.ToJson());
            forEachContext.Set("content", context.Result.ToString()); // todo advanced condition like amount of time, etc.

            // set the name of the Parameters key to give each `item` in the foreach a name
            forEachContext.Set("Parameters", "input"); // This is skill specific
            var result = await this._processSkillKernel.RunAsync(forEachContext, this._forEachSkill["ForEach"]);
            if (result.Variables.Get("FOREACH_RESULT", out var forEachResultPlan))
            {
                plan = Plan.FromJson(forEachResultPlan.ToString(), context);
                plan.Name = $"{processName} Buddy";
                plan.Description = processDescription;
            }
        }

        var processPlanJson = plan.ToJson();
        var ProcessPlanstring = processPlanJson; //ProcessSkill.ToPlanString(plan);
        var formattedPlanString = ProcessSkill.ToPlanString(plan);
        // TODO - let's get rid of some state like 'action' and stuff before serializing. How?
        context.Variables.Update(ProcessPlanstring);
        context.Variables.Set("processPlanJson", processPlanJson);

        // TODO DO I need to wrap this in a call to InstructProcess? How did LearningSkill do this? It was messy... sorry.
        //

        // chat/bot support
        await context.Memory.SaveInformationAsync(
            collection: $"{chatId}-ProcessSkill.ProcessPlans",
            text: formattedPlanString,
            id: Guid.NewGuid().ToString(),
            description: $"Process plan about {processName}",
            additionalMetadata: processPlanJson);

        return context;
    }

    // Instruct a process plan
    // - Select steps that require action, sequentially run them with the user.
    // - Returns ActOnMessage results (set the action to this which will run the next step and return message to user, or unset when the steps are done)
    // - [Stretch] If no steps are in need of user input, then generate status message (You need to do something like upload results, etc.)
    [SKFunctionName("InstructProcessPlan")]
    [SKFunction("Instruct a process plan")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Chat message history", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "userId", Description = "ID of the user who owns the documents", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "userIntent", Description = "The user intent", DefaultValue = "")]
    // TODO: maybe userIntent instead of "lesson" query
    public async Task<SKContext> InstructProcessAsync(SKContext context)
    {
        // get chatId and search memory for Process plans
        // if there are no Process plans, then return a message saying that there are no Process plans
        // if there are Process plans, then return a message with the Process plans
        // Ask them to choose for action
        // if there is a Process plan, then run it ( will need user message as input likely )
        if (context.Variables.Get("chatId", out var chatId))
        {
            // TODO - For now just pick 1
            var memories = context.Memory.SearchAsync($"{chatId}-ProcessSkill.ProcessPlans", query: "process", limit: 1, minRelevanceScore: 0.0).ToEnumerable()
                .ToList();
            if (memories.Count == 0)
            {
                context.Variables.Update("You don't have any Process plans. Create one with `create lesson`");
                return context;
            }

            // todo error handling
            var processPlanJson = memories[0].Metadata.AdditionalMetadata;
            var processPlan = Plan.FromJson(processPlanJson, context);
            var processStepIndex = processPlan.Steps[processPlan.NextStepIndex].NextStepIndex;
            Console.WriteLine($"Process step index: {processStepIndex}");

            var processDescription = processPlan.Description;
            var processName = processPlan.Name;
            var processContext = processDescription;
            var userIntent = context.Variables["userIntent"];
            context.Variables.Get("userId", out var userId);
            context.Variables.Get("tokenLimit", out var tokenLimit);
            context.Variables.Get("contextTokenLimit", out var contextTokenLimit);

            if (context.Skills is not null && context.Skills.TryGetFunction("DocumentMemorySkill", "QueryDocuments", out var queryDocuments))
            {
                var documentContext = new ContextVariables(string.Join(" ", processName, processDescription));
                documentContext.Set("userId", userId);
                documentContext.Set("tokenLimit", tokenLimit);
                documentContext.Set("contextTokenLimit", contextTokenLimit);
                var queryDocumentsContext = new SKContext(documentContext, context.Memory, context.Skills, context.Log, context.CancellationToken);

                var queryDocumentsResult = await queryDocuments.InvokeAsync(queryDocumentsContext);
                processContext = queryDocumentsResult.Result.ToString();
                if (string.IsNullOrEmpty(processContext))
                {
                    Console.WriteLine($"*understands* I didn't find any documents about {processName}");
                }
                else
                {
                    Console.WriteLine($"*understands* I found {processContext}");
                }
            }
            else
            {
                Console.WriteLine("DocumentMemorySkill not found");
            }

            // TODO: Make this better
            context.Variables.Set("context", processContext); // {DocumentMemorySkill.QueryDocuments $INPUT}

            var plan = await processPlan.InvokeNextStepAsync(context);
            if (plan is not null)
            {
                var result = plan.State.ToString();
                // Requirement: Steps "LESSON_STATE" must be set to "IN_PROGRESS"|empty or "DONE"
                if (!processPlan.HasNextStep || !processPlan.Steps[processPlan.NextStepIndex].HasNextStep)
                {
                    Console.WriteLine("No more steps in plan");
                    context.Variables.Set("continuePlan", null);
                    context.Variables.Set("updatePlan", null);
                }
                else if (!processPlan.Steps[processPlan.NextStepIndex].Steps[processStepIndex].State.Get("LESSON_STATE", out var lessonState))
                {
                    Console.WriteLine("LESSON_STATE not found, continue plan");
                    context.Variables.Set("continuePlan", "true");
                }
                else // if (lessonState == "DONE")
                {
                    if (lessonState == "DONE")
                    {
                        // continue with next step in plan.
                        Console.WriteLine($"Continuing with next step in Process plan and updating memory: {plan.HasNextStep}");
                        context.Variables.Set("continuePlan", plan.HasNextStep.ToString());
                        context.Variables.Set("updatePlan", "true");

                        // Update the plan in memory
                        var ProcessPlanstring = plan.ToJson();
                        var formattedProcessPlanstring = ProcessSkill.ToPlanString(plan);
                        await context.Memory.SaveInformationAsync(
                            collection: $"{chatId}-ProcessSkill.ProcessPlans",
                            text: formattedProcessPlanstring,
                            id: memories[0].Metadata.Id,
                            description: $"In Progress {memories[0].Metadata.Description}",
                            additionalMetadata: ProcessPlanstring);
                    }
                    else
                    {
                        Console.WriteLine("LESSON_STATE was not DONE, continue plan");
                        context.Variables.Set("continuePlan", "true");
                        context.Variables.Set("updatePlan", "false");
                    }
                }

                context.Variables.Update(result);
            }
            else
            {
                Console.WriteLine("Clearing action from context 2");
                context.Variables.Set("continuePlan", null);
                context.Variables.Set("updatePlan", null);
            }
        }
        else
        {
            context.Variables.Update("You don't have a chatId. Please set one with `set chatId`");
        }

        Console.WriteLine("Done with instruction step");
        return context;
    }

    // ListProcessPlans
    [SKFunctionName("ListProcessnPlans")]
    [SKFunctionContextParameter(Name = "processName", Description = "Name of the process")]
    [SKFunctionContextParameter(Name = "processDescription", Description = "Description of the process")]
    [SKFunction("List known process plans")]
    public Task<SKContext> ListProcessPlansAsync(SKContext context)
    {
        var memories = context.Memory.SearchAsync("ProcessSkill.ProcessPlans", query: "process", limit: 10, minRelevanceScore: 0.0).ToEnumerable().ToList();

        context.Variables.Update(string.Join("\n", memories.Select(m => $"[{m.Metadata.Id}]{m.Metadata.Text}")));

        return Task.FromResult(context);
    }

    internal static readonly List<string> lessonMarkers = new() { "Process:", "Objective:", "Topic:", "Requirements:" };

    internal static string ToPlanString(Plan originalPlan, string indent = " ", int lessonMarkerIndex = 0)
    {
        var next = lessonMarkerIndex + 1;
        string stepItems = string.Join("\n", originalPlan.Steps.Select(step =>
        {
            if (step.Steps.Count == 0)
            {
                string skillName = step.SkillName;
                string stepName = step.Name;

                string namedParams = string.Join(" ", step
                    .Parameters
                    .Where(param => !string.IsNullOrEmpty(param.Value) || !param.Key.Equals("INPUT", StringComparison.OrdinalIgnoreCase))
                    .Select(param => $"{param.Key}='{param.Value}'"));
                if (!string.IsNullOrEmpty(namedParams))
                {
                    namedParams = $" {namedParams}";
                }

                string? namedOutputs = step.Outputs?.Select(output => output).FirstOrDefault();
                if (!string.IsNullOrEmpty(namedOutputs))
                {
                    namedOutputs = $" => {namedOutputs}";
                }

                return $"{indent}{indent}- {string.Join(".", skillName, stepName)}{namedParams}{namedOutputs}";
            }
            else
            {
                string nestedSteps = ToPlanString(step, indent + indent, next);
                return nestedSteps;
            }
        }));

        string state = string.Join("\n", originalPlan.State
            .Where(pair => !string.IsNullOrEmpty(pair.Value) || !pair.Key.Equals("INPUT", StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"{indent}{indent}{pair.Key} = {pair.Value}"));
        var prefix = lessonMarkerIndex == 0 ? "Generated process plan.\n" : string.Empty;
        return $"{prefix}{indent}{lessonMarkers[Math.Min(lessonMarkerIndex, lessonMarkers.Count - 1)]} {originalPlan.Description}\n{stepItems}";
    }
}
