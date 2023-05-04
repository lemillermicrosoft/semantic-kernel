// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using RepoUtils;

namespace Skills;


public class ChatAgentSkill
{
    private readonly IKernel _chatAgentSkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _chatSkill;
    private readonly IDictionary<string, ISKFunction> _chatAgentSkill;

    public ChatAgentSkill()
    {
        // Create a kernel
        this._chatAgentSkillKernel = KernelUtils.CreateKernel();

        string folder = RepoFiles.SampleSkillsPath();
        this._semanticSkills = this._chatAgentSkillKernel.ImportSemanticSkillFromDirectory(folder,
            "ChatAgentSkill",
            "DoWhileSkill");

        this._chatAgentSkill = this._chatAgentSkillKernel.ImportSkill(this, "ChatAgentSkill");

        this._chatSkill = this._chatAgentSkillKernel.ImportSkill(new ChatSkill((context) =>
        {
            var line = $"Chat Agent: {context.Variables.Input.Trim()}";
            Console.WriteLine(line);
            context.Variables.Update(line);
            context.Variables.Get("chat_history", out var chatHistory);
            context.Variables.Set("chat_history", $"{chatHistory}\n{line}");
            Console.Write("User: ");
            return Task.FromResult(context);
        }, (context) =>
        {
            var line = Console.ReadLine();
            context.Variables.Update($"User: {line}");
            context.Variables.Get("chat_history", out var chatHistory);
            context.Variables.Set("chat_history", $"{chatHistory}\nUser: {line}");
            return Task.FromResult(context);
        }), "ChatSkill");

        this._doWhileSkill = this._chatAgentSkillKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");
    }

    // StudySession
    [SKFunction(description: "Chat session")]
    [SKFunctionName("ChatSession")]
    // [SKFunctionContextParameter(Name = "topic", Description = "Topic to study e.g. 'Equations and inequalities'")]
    // [SKFunctionContextParameter(Name = "course", Description = "The course of study e.g. 'Algebra 1'")]
    public async Task<SKContext> ChatSessionAsync(SKContext context)
    {
        //
        // Create a lesson to read
        //
        var lessonFunction = this._semanticSkills["CreateChat"]; // input, course, TODO context
        var lessonContext = context.Variables.Clone();
        if (context.Variables.Get("topic", out var topic))
        {
            lessonContext.Update(topic);
        }
        else
        {
            topic = context.Variables.Input;
            context.Variables.Set("topic", topic);
            lessonContext.Update(topic);
        }

        var lctx = this._chatAgentSkillKernel.CreateNewContext();

        foreach (KeyValuePair<string, string> x in lessonContext)
        {
            lctx.Variables[x.Key] = x.Value;
        }

        var lessonStart = await lessonFunction.InvokeAsync(lctx);

        var doWhileContext = new ContextVariables(lessonStart.Result);
        doWhileContext.Set("message", lessonStart.Result);
        doWhileContext.Set("topic", topic);
        if (context.Variables.Get("course", out var course))
        {
            doWhileContext.Set("course", course);
        }

        // Create a plan to chat with the user until they say goodbye
        var plan = new Plan("Prepare a message and send it.");
        plan.Outputs.Add("course");
        plan.Outputs.Add("topic");
        plan.Outputs.Add("context");
        var prepareStep = new Plan(this._chatAgentSkill["ActOnMessage"]);
        prepareStep.Outputs.Add("chat_history");
        var sendStep = new Plan(this._chatSkill["SendMessage"]);
        sendStep.Outputs.Add("chat_history");

        plan.AddSteps(prepareStep, sendStep);
        doWhileContext.Set("action", plan.ToJson());
        doWhileContext.Set("condition", "User does not say 'goodbye'"); // todo advanced condition like amount of time, etc.

        return await this._chatAgentSkillKernel.RunAsync(doWhileContext, this._doWhileSkill["DoWhile"]);
    }

    // ActOnMessage
    [SKFunction(description: "ActOnMessage")]
    [SKFunctionName("ActOnMessage")]
    [SKFunctionContextParameter(Name = "message", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Message to send")]
    public async Task<SKContext> ActOnMessageAsync(SKContext context)
    {
        // If there is a message, use it. Otherwise, get the chat history and generate completion for next message.
        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            // course, chat_history, topic, context

            // TODO Use actionPlanner to either ContinueChat or StartStudyAgent

            var completion = await this._semanticSkills["ContinueLesson"].InvokeAsync(context);
            context.Variables.Update(completion.Result);
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
