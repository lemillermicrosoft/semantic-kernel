// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using RepoUtils;

namespace Skills;

// Depends on: ForEachSkill
// Context requires: StudySkill
public class LearningSkill
{
    // private readonly IKernel _kernel;
    // private readonly IDictionary<string, ISKFunction> _semanticSkills;

    private readonly IKernel _learningSkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;

    private readonly IDictionary<string, ISKFunction> _forEachSkill;
    // private readonly IDictionary<string, ISKFunction> _studySkill;

    #region create a kernel

    public LearningSkill() // todo callback for OnLessonAdded
    {
        // Create a kernel
        this._learningSkillKernel = KernelUtils.CreateKernel();

        #endregion

        string folder = RepoFiles.SampleSkillsPath();
        this._semanticSkills = this._learningSkillKernel.ImportSemanticSkillFromDirectory(folder,
            // "StudySkill",
            "ForEachSkill");

        // this._studySkill = this._learningSkillKernel.ImportSkill(this, "StudySkill");

        this._forEachSkill = this._learningSkillKernel.ImportSkill(new ForEachSkill(this._semanticSkills["ToList"]), "ForEachSkill");
    }

    // public LearningSkill(IKernel kernel)
    // {
    //     this._kernel = kernel;

    //     string folder = RepoFiles.SampleSkillsPath();
    //     this._semanticSkills = this._kernel.ImportSemanticSkillFromDirectory(folder, "ChatAgentSkill");

    // }

    // GatherKnowledge (docs, web, generated)

    // CreateInstructions (based on Knowledge)

    // CreateEvaluations (based on Knowledge)

    // ConverseAboutLesson (based on lesson plan)

    // So the 'agent' plan is to:
    // 1. Gather knowledge

    // 4. Converse about lesson

    // The 'lesson' plan is to:
    // 1. Create instructions
    // 2. Create evaluations

    // CreateLesson
    [SKFunctionName("CreateLesson")]
    [SKFunctionContextParameter(Name = "lessonName", Description = "Name of the lesson")]
    [SKFunctionContextParameter(Name = "lessonDescription", Description = "Description of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonType", Description = "Type of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonLevel", Description = "Level of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonSubject", Description = "Subject of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonTopic", Description = "Topic of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonSubTopic", Description = "SubTopic of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonLanguage", Description = "Language of the lesson")]
    // [SKFunctionContextParameter(Name = "lessonLocale", Description = "Locale of the lesson")]
    [SKFunction("Create a lesson")]
    public async Task<SKContext> CreateLessonAsync(SKContext context)
    {
        // TODO - This skill could be a chatbot that asks questions for the steps it takes to create a lesson

        // TODO: Create a lesson
        // Crawl - simple ack - "I see you want to create a lesson about..." *done*
        // Walk - Create a lesson plan and return serialized plan *done*
        // Speed walk - Create a lesson plan using memories TODO
        // Run - Create and save a lesson in memory *done*
        // Final -- Finalize the plan being created (Instruct/Evaluate/Converse) TODO
        context.Variables.Get("lessonName", out var lessonName);
        context.Variables.Get("lessonDescription", out var lessonDescription);

        // var plan = new Plan(lessonDescription);
        // plan.State.Set("course", lessonName);
        // plan.State.Set("context", lessonDescription); // TODO: get context from memory
        // if (context.Skills is not null &&
        //     context.Skills.TryGetFunction("StudySkill", "CreateLessonTopics", out var createLessonTopics) &&
        //     // context.Skills.TryGetFunction("StudySkill", "SelectLessonTopic", out var selectLessonTopic) &&
        //     context.Skills.TryGetFunction("StudySkill", "StudySession", out var studySession))
        // {
        //     // plan.AddSteps(semanticSkills["CreateLessonTopics"], semanticSkills["SelectLessonTopic"], studySKill["StudySession"]);
        //     plan.AddSteps(createLessonTopics!, selectLessonTopic!, studySession!);
        // }
        Console.WriteLine($"*understands* I see you want to create a lesson about {lessonName}");

        var plan = new Plan(lessonDescription);
        if (context.Skills?.TryGetFunction("StudySkill", "CreateLessonTopics", out var createLessonTopics) == true)
        {
            context.Variables.Update(lessonName);
            context.Variables.Set("context", lessonDescription); // todo make this better
            context = await createLessonTopics.InvokeAsync(context);

            var studyPlan = new Plan(lessonDescription); // todo maybe use the output of createLessonTopics too
            // plan.State.Set("course", lessonName);
            // plan.State.Set("context", lessonDescription); // TODO: get context from memory
            if (context.Skills is not null &&
                // context.Skills.TryGetFunction("StudySkill", "CreateLessonTopics", out var createLessonTopics) &&
                // context.Skills.TryGetFunction("StudySkill", "SelectLessonTopic", out var selectLessonTopic) &&
                context.Skills.TryGetFunction("StudySkill", "StudySession", out var studySession))
            {
                // plan.AddSteps(semanticSkills["CreateLessonTopics"], semanticSkills["SelectLessonTopic"], studySKill["StudySession"]);
                // plan.AddSteps(createLessonTopics!, selectLessonTopic!, studySession!);
                studyPlan.AddSteps(studySession!);
            }

            var forEachContext = new ContextVariables(context.Result.ToString());
            // forEachContext.Set("message", lessonStart.Result);
            // forEachContext.Set("topic", topic);
            // if (context.Variables.Get("course", out var course))
            // {
            //     forEachContext.Set("course", course);
            // }

            forEachContext.Set("goalLabel", "Complete Lessons");
            forEachContext.Set("stepLabel", "Complete Lesson");
            forEachContext.Set("action", studyPlan.ToJson());
            forEachContext.Set("content", context.Result.ToString()); // todo advanced condition like amount of time, etc.
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

        await context.Memory.SaveInformationAsync(
            collection: "LearningSkill.LessonPlans",
            text: lessonPlanString,
            // text: lessonPlanJson,
            id: Guid.NewGuid().ToString(),
            description: $"Lesson plan about {lessonName}",
            additionalMetadata: lessonPlanJson);

        return context;
    }

    // InstructSession
    [SKFunctionName("InstructSession")]
    [SKFunction("Instruct a session")]
    public Task<SKContext>? InstructSessionAsync(SKContext context)
    {
        return null;
    }

    // EvaluateSession

    // LessonConversation

    // I want to start that learning plan (RunLearningPlan) --> Return Choices (Instruct, Evaluate, Converse) --> Selection
    //      RunLearningPLan -> Do (give choices)  While (choice is not made)

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
