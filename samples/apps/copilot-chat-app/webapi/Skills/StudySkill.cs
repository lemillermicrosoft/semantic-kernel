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
    [SKFunctionContextParameter(Name = "LESSON_STATE", Description = "State of the study session")]
    [SKFunctionContextParameter(Name = "context", Description = "Contextual information for the study session")]
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
    [SKFunctionContextParameter(Name = "LESSON_STATE", Description = "State of the study session")]
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
            string message = "";
            string evaluationScore = "";
            try
            {
                var completionObject = JObject.Parse(completion.Result);
                message = completionObject!["message"].ToString();
                evaluationScore = completionObject!["evaluationScore"].ToString();
            }
            catch (Exception e)
            {
                // sometimes it's ending the json prematurely... append ", "evaluationScore": 1}" to the end and try again, otherwise just use the whole thing
                Console.WriteLine($"Error parsing completion: {e.Message}");
                try
                {
                    var completionObject = JObject.Parse(completion.Result + ", \"evaluationScore\": 1}");
                    message = completionObject!["message"].ToString();
                    evaluationScore = completionObject!["evaluationScore"].ToString();
                }
                catch (Exception e2)
                {
                    Console.WriteLine($"Error parsing completion: {e2.Message}");
                    message = completion.Result;
                    evaluationScore = "1"; // assumption
                }
            }


            context.Variables.Update(message);
            context.Variables.Set("evaluationScore", evaluationScore);

            // get float from evaluationScore and see if greater than 0.9
            Console.WriteLine($"Evaluation score: {evaluationScore}");
            if (float.TryParse(evaluationScore, out var evaluationScoreFloat))
            {
                if (evaluationScoreFloat > 0.99)
                {
                    Console.WriteLine("Lesson is done!");
                    context.Variables.Set("LESSON_STATE", "DONE");
                }
                else
                {
                    context.Variables.Set("LESSON_STATE", "IN_PROGRESS");
                }
            }
            else
            {
                context.Variables.Set("LESSON_STATE", "IN_PROGRESS");
            }
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
