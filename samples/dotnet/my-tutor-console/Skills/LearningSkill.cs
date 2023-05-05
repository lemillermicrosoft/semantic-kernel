// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Skills;

public class LearningSkill
{
    // private readonly IKernel _kernel;
    // private readonly IDictionary<string, ISKFunction> _semanticSkills;

    public LearningSkill() // todo callback for OnLessonAdded
    {
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
        // TODO: Create a lesson
        // Crawl - simple ack - "I see you want to create a lesson about..."
        // Walk - Create a lesson plan and return serialized plan
        // Speed walk - Create a lesson plan using memories
        // Run - Create and save a lesson in memory
        context.Variables.Get("lessonName", out var lessonName);
        context.Variables.Get("lessonDescription", out var lessonDescription);

        var plan = new Plan(lessonDescription);
        plan.State.Set("course", lessonName);
        plan.State.Set("context", lessonDescription); // TODO: get context from memory
        if (context.Skills is not null &&
            context.Skills.TryGetFunction("StudySkill", "CreateLessonTopics", out var createLessonTopics) &&
            context.Skills.TryGetFunction("StudySkill", "SelectLessonTopic", out var selectLessonTopic) &&
            context.Skills.TryGetFunction("StudySkill", "StudySession", out var studySession))
        {
            // plan.AddSteps(semanticSkills["CreateLessonTopics"], semanticSkills["SelectLessonTopic"], studySKill["StudySession"]);
            plan.AddSteps(createLessonTopics!, selectLessonTopic!, studySession!);
        }

        Console.WriteLine($"*understands* I see you want to create a lesson about {lessonName}");
        // context.Variables.Update(lessonPlanString);

        var lessonPlanJson = plan.ToJson();
        context.Variables.Update(LearningSkill.ToPlanString(plan));
        context.Variables.Set("lessonPlanJson", lessonPlanJson);

        await context.Memory.SaveInformationAsync(
            collection: "LearningSkill.LessonPlans",
            text: lessonPlanJson,
            id: Guid.NewGuid().ToString(),
            description: $"Lesson plan about {lessonName}",
            additionalMetadata: null);

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

    internal static string ToPlanString(Plan originalPlan, string indent = " ")
    {
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
                string nestedSteps = ToPlanString(step, indent + indent);
                return nestedSteps;
            }
        }));

        string state = string.Join("\n", originalPlan.State
            .Where(pair => !string.IsNullOrEmpty(pair.Value) || !pair.Key.Equals("INPUT", StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"{indent}{indent}{pair.Key} = {pair.Value}"));

        return $"\n{indent}Goal: {originalPlan.Description}\n\n{indent}State:\n{state}\n\n{indent}Steps:\n{stepItems}";
    }

    // InstructSession

    // EvaluateSession

    // LessonConversation
}
