// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json.Linq;

namespace SemanticKernel.Service.Skills;

public class StudySkill
{
    private readonly IKernel _studySkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _studySkill;

    public StudySkill(IKernel kernel)
    {
        #region create a kernel

        // Create a kernel
        this._studySkillKernel = kernel;

        #endregion

        this._semanticSkills = kernel.RegisterNamedSemanticSkills(null, null, "StudySkill", "DoWhileSkill");

        this._studySkill = this._studySkillKernel.ImportSkill(this, "StudySkill");

        this._doWhileSkill = this._studySkillKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");
    }

    // StudySession
    [SKFunction(description: "Study session")]
    [SKFunctionName("StudySession")]
    [SKFunctionContextParameter(Name = "topic", Description = "Topic to study e.g. 'Equations and inequalities'")]
    [SKFunctionContextParameter(Name = "course", Description = "The course of study e.g. 'Algebra 1'")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Chat message history")]
    public async Task<SKContext> StudySessionAsync(SKContext context)
    {
        //
        // Create a lesson to read
        //
        var studySessionContext = context.Variables.Clone();
        var lessonFunction = this._semanticSkills["CreateLesson"];
        if (context.Variables.Get("topic", out var topic))
        {
            studySessionContext.Update(topic);
        }
        else
        {
            topic = context.Variables.Input;
            context.Variables.Set("topic", topic);
            studySessionContext.Update(topic);
        }

        context.Variables.Get("course", out var course);
        course ??= "Unknown";

        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            await this._studySkill["PrepareMessage"].InvokeAsync(context);

            // TODO - When are we done now?
        }
        else
        {
            Console.WriteLine($"Starting study session on {topic} for {course}.");
            var createLessonContext = this._studySkillKernel.CreateNewContext();
            foreach (KeyValuePair<string, string> x in studySessionContext)
            {
                createLessonContext.Variables[x.Key] = x.Value;
            }

            var lessonStart = await lessonFunction.InvokeAsync(createLessonContext);

            context.Variables.Update(lessonStart.Result);
        }

        return context;
    }

    // PrepareMessage
    [SKFunction(description: "Prepare a message")]
    [SKFunctionName("PrepareMessage")]
    [SKFunctionContextParameter(Name = "message", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "topic", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "context", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "course", Description = "Message to send")]
    public async Task<SKContext> PrepareMessageAsync(SKContext context)
    {
        // If there is a message, use it. Otherwise, get the chat history and generate completion for next message.
        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            // course, chat_history, topic, context
            var completion = await this._semanticSkills["ContinueLesson"].InvokeAsync(context);

            Console.WriteLine($"Completion: {completion.Result}");

            // completion is now a JSON object e.g. {"message": "What is the answer to 2+2?", "evaluationScore": 0.2}
            // parse completion
            var completionObject = JObject.Parse(completion.Result);

            context.Variables.Update(completionObject!["message"].ToString());
            context.Variables.Set("evaluationScore", completionObject!["evaluationScore"].ToString());
            // context.Variables.Update(completion.Result);
        }
        else if (context.Variables.Get("message", out var message))
        {
            context.Variables.Update(message);
        }
        else
        {
            // TODO: Get the chat history and generate completion for next message.
        }

        return context;
    }
}
