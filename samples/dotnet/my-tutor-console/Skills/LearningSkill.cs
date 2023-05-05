// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Skills;


public class LearningSkill
{
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
    public Task<SKContext> CreateLessonAsync(SKContext context)
    {
        // TODO: Create a lesson
        // Crawl - simple ack - "I see you want to create a lesson about..."
        // Walk - Create a lesson plan and return serialized plan
        // Speed walk - Create a lesson plan using memories
        // Run - Create and save a lesson in memory
        context.Variables.Get("lessonName", out var lessonName);
        context.Variables.Get("lessonDescription", out var lessonDescription);
        context.Variables.Update("I see you want to create a lesson about " + lessonName + ". " + lessonDescription + "");

        return Task.FromResult(context);
    }


    // InstructSession

    // EvaluateSession

    // LessonConversation
}
