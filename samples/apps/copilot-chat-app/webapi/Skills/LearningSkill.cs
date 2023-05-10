// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

// Depends on: ForEachSkill
// Context requires: StudySkill
public class LearningSkill
{
    private readonly IKernel _learningSkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;

    private readonly IDictionary<string, ISKFunction> _forEachSkill;

    #region create a kernel

    public LearningSkill(IKernel kernel)
    {
        // Create a kernel
        this._learningSkillKernel = kernel;

        #endregion

        string folder = FunctionLoadingExtensions.SampleSkillsPath();
        this._semanticSkills = this._learningSkillKernel.ImportSemanticSkillFromDirectory(folder,
            "ForEachSkill");

        this._forEachSkill = this._learningSkillKernel.ImportSkill(new ForEachSkill(this._semanticSkills["ToList"]), "ForEachSkill");
    }

    // CreateLesson
    [SKFunctionName("CreateLesson")]
    [SKFunctionContextParameter(Name = "lessonName", Description = "Name of the lesson")]
    [SKFunctionContextParameter(Name = "lessonDescription", Description = "Description of the lesson")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat")]
    [SKFunction("Create a lesson")]
    public async Task<SKContext> CreateLessonAsync(SKContext context)
    {
        context.Variables.Get("lessonName", out var lessonName);
        context.Variables.Get("lessonDescription", out var lessonDescription);

        Console.WriteLine($"*understands* I see you want to create a lesson about {lessonName}");

        var plan = new Plan(lessonDescription);
        if (context.Skills?.TryGetFunction("StudySkill", "CreateLessonTopics", out var createLessonTopics) == true)
        {
            context.Variables.Update(lessonName);
            // TODO: Make this better
            context.Variables.Set("context", lessonDescription);
            var course = context.Variables.Input;
            context = await createLessonTopics.InvokeAsync(context);

            // TODO: maybe use the output of createLessonTopics too
            var studyPlan = new Plan(lessonDescription);
            studyPlan.State.Set("course", course);
            if (context.Skills is not null &&
                context.Skills.TryGetFunction("StudySkill", "StudySession", out var studySession))
            {
                studyPlan.AddSteps(studySession!);
            }

            var forEachContext = new ContextVariables(context.Result.ToString());

            forEachContext.Set("goalLabel", "Complete Lessons");
            forEachContext.Set("stepLabel", "Complete Lesson");
            forEachContext.Set("action", studyPlan.ToJson());
            forEachContext.Set("content", context.Result.ToString()); // todo advanced condition like amount of time, etc.

            // set the name of the Parameters key to give each `item` in the foreach a name
            forEachContext.Set("Parameters", "topic"); // This is skill specific
            var result = await this._learningSkillKernel.RunAsync(forEachContext, this._forEachSkill["ForEach"]);
            if (result.Variables.Get("FOREACH_RESULT", out var forEachResultPlan))
            {
                plan.AddSteps(Plan.FromJson(forEachResultPlan.ToString(), context)!);
            }
        } // else say that we can't

        var lessonPlanJson = plan.ToJson();
        var lessonPlanString = LearningSkill.ToPlanString(plan);
        context.Variables.Update(lessonPlanString);
        context.Variables.Set("lessonPlanJson", lessonPlanJson);

        // chat/bot support
        context.Variables.Get("chatId", out var chatId);

        await context.Memory.SaveInformationAsync(
            collection: $"{chatId}-LearningSkill.LessonPlans",
            text: lessonPlanString,
            id: Guid.NewGuid().ToString(),
            description: $"Lesson plan about {lessonName}",
            additionalMetadata: lessonPlanJson);

        return context;
    }

    // Instruct a lesson
    // - Select steps that require action, sequentially run them with the user.
    // - Returns ActOnMessage results (set the action to this which will run the next step and return message to user, or unset when the steps are done)
    // - [Stretch] If no steps are in need of user input, then generate status message (You need to do something like upload results, etc.)
    [SKFunctionName("InstructLesson")]
    [SKFunction("Instruct a lesson plan")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat", DefaultValue = "")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Chat message history")]
    // TODO: maybe userIntent instead of "lesson" query
    public async Task<SKContext> InstructSessionAsync(SKContext context)
    {
        // get chatId and search memory for lesson plans
        // if there are no lesson plans, then return a message saying that there are no lesson plans
        // if there are lesson plans, then return a message with the lesson plans
        // Ask them to choose for action
        // if there is a lesson plan, then run it ( will need user message as input likely )
        if (context.Variables.Get("chatId", out var chatId))
        {
            // TODO - For now just pick 1
            var memories = context.Memory.SearchAsync($"{chatId}-LearningSkill.LessonPlans", query: "lesson", limit: 1, minRelevanceScore: 0.0).ToEnumerable()
                .ToList();
            if (memories.Count == 0)
            {
                context.Variables.Update("You don't have any lesson plans. Create one with `create lesson`");
                return context;
            }

            var lessonPlanJson = memories[0].Metadata.AdditionalMetadata;
            var lessonPlan = Plan.FromJson(lessonPlanJson, context);
            var lessonStepIndex = lessonPlan.NextStepIndex;

            // PROBLEM It's doing all of the lessons. It should only do the next one.
            var plan = await lessonPlan.InvokeNextStepAsync(context);
            if (plan is not null)
            {
                var result = plan.State.ToString();
                // Requirement: Steps "LESSON_STATE" must be set to "IN_PROGRESS"|empty or "DONE"
                if (!plan.Steps[lessonStepIndex].State.Get("LESSON_STATE", out var lessonState) || lessonState == "IN_PROGRESS")
                {
                    context.Variables.Set("continuePlan", "true");
                }
                else // if (lessonState == "DONE")
                {
                    // continue with next step in plan.
                    context.Variables.Set("continuePlan", null);
                }
                Console.WriteLine($"Lesson state: {result}");
                context.Variables.Update(result);
            }
            else
            {
                Console.WriteLine("Clearing action from context 2");
                context.Variables.Set("continuePlan", null);
            }
        }
        else
        {
            context.Variables.Update("You don't have a chatId. Please set one with `set chatId`");
        }

        Console.WriteLine("Done with instruction step");
        return context;
    }

    // ListLessonPlans
    [SKFunctionName("ListLessonPlans")]
    [SKFunctionContextParameter(Name = "lessonName", Description = "Name of the lesson")]
    [SKFunctionContextParameter(Name = "lessonDescription", Description = "Description of the lesson")]
    [SKFunction("List known lesson plans")]
    public Task<SKContext> ListLessonPlansAsync(SKContext context)
    {
        var memories = context.Memory.SearchAsync("LearningSkill.LessonPlans", query: "lesson", limit: 10, minRelevanceScore: 0.0).ToEnumerable().ToList();

        context.Variables.Update(string.Join("\n", memories.Select(m => $"[{m.Metadata.Id}]{m.Metadata.Text}")));

        return Task.FromResult(context);
    }

    internal static readonly List<string> lessonMarkers = new() { "Lesson:", "Objective:", "Topic:", "Course:" };

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
        var prefix = lessonMarkerIndex == 0 ? "Generated learning plan.\n" : string.Empty;
        return $"{prefix}{indent}{lessonMarkers[Math.Min(lessonMarkerIndex, lessonMarkers.Count - 1)]} {originalPlan.Description}\n{stepItems}";
    }
}
