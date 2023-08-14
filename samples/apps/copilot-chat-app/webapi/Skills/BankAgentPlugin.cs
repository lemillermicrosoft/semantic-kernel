// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json.Linq;

namespace SemanticKernel.Service.Skills;

public class BankAgentPlugin
{
    private readonly IKernel _gatherRequirementsPluginKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _gatherRequirementsPlugin;

    public BankAgentPlugin(IKernel kernel)
    {
        #region create a kernel

        // Create a kernel
        this._gatherRequirementsPluginKernel = kernel;

        #endregion

        this._semanticSkills = kernel.RegisterNamedSemanticSkills(null, null, "BankAgentPlugin", "DoWhileSkill");

        this._gatherRequirementsPlugin = this._gatherRequirementsPluginKernel.ImportSkill(this, "BankAgentPlugin");

        this._doWhileSkill = this._gatherRequirementsPluginKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");
    }

    [SKFunction(description: "Agent chat conversation on gathering requirements for a process.")]
    [SKFunctionName("GatherProcessRequirements")]
    [SKFunctionContextParameter(Name = "input", Description = "Requirements to focus on gathering e.g. 'contact details'")]
    [SKFunctionContextParameter(Name = "process", Description = "The process e.g. 'new savings account'")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Chat message history")]
    [SKFunctionContextParameter(Name = "LESSON_STATE", Description = "State of the gathering session")]
    [SKFunctionContextParameter(Name = "context", Description = "Contextual information for the gathering session")]
    public async Task<SKContext> GatherProcessRequirementsAsync(SKContext context)
    {
        //
        // Create a lesson to read
        //
        var requirementsGatheringContext = context.Variables.Clone();
        var lessonFunction = this._semanticSkills["GatherRequirements"];
        if (context.Variables.Get("input", out var input))
        {
            requirementsGatheringContext.Update(input);
        }
        else
        {
            input = context.Variables.Input;
            context.Variables.Set("topic", input);
            requirementsGatheringContext.Update(input);
        }

        context.Variables.Get("process", out var process);
        process ??= "Unknown";

        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            await this._gatherRequirementsPlugin["PrepareMessage"].InvokeAsync(context);

            // TODO - When are we done now?
        }
        else
        {
            Console.WriteLine($"Starting requirements gathering session on {input} for {process}.");
            var createLessonContext = this._gatherRequirementsPluginKernel.CreateNewContext();
            foreach (KeyValuePair<string, string> x in requirementsGatheringContext)
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
    [SKFunctionContextParameter(Name = "input", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "context", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "process", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "LESSON_STATE", Description = "State of the study session")]
    public async Task<SKContext> PrepareMessageAsync(SKContext context)
    {
        // If there is a message, use it. Otherwise, get the chat history and generate completion for next message.
        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            // process, chat_history, topic, context
            var completion = await this._semanticSkills["ContinueLesson"].InvokeAsync(context);

            Console.WriteLine($"Completion: {completion.Result}");

            // completion is now a JSON object e.g. {"message": "What is the answer to 2+2?", "gatherScore": 0.2}
            // parse completion
            string? message = "";
            string? gatherScore = "";
            try
            {
                var completionObject = JObject.Parse(completion.Result);
                message = completionObject?["message"]?.ToString();
                gatherScore = completionObject?["gatherScore"]?.ToString();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
            {
                // sometimes it's ending the json prematurely... append ", "gatherScore": 1}" to the end and try again, otherwise just use the whole thing
                Console.WriteLine($"Error parsing completion: {e.Message}");
                try
                {
                    var completionObject = JObject.Parse(completion.Result + ", \"gatherScore\": 1}");
                    message = completionObject?["message"]?.ToString();
                    gatherScore = completionObject?["gatherScore"]?.ToString();
                }
                catch (Exception e2)
                {
                    Console.WriteLine($"Error parsing completion: {e2.Message}");
                    message = completion.Result;
                    gatherScore = "1"; // assumption
                }
            }
#pragma warning restore CA1031 // Do not catch general exception types

            context.Variables.Update(message!);
            context.Variables.Set("gatherScore", gatherScore);

            // get float from gatherScore and see if greater than 0.9
            Console.WriteLine($"Gather score: {gatherScore}");
            if (float.TryParse(gatherScore, out var gatherScoreFloat))
            {
                if (gatherScoreFloat > 0.99)
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
