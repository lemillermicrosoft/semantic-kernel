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

public class ChatAgent
{
    private readonly IKernel _chatAgentKernel;
    private readonly IKernel _actionKernel;
    private readonly IDictionary<string, ISKFunction> _chatSkill;
    private readonly Plan _chatPlan;
    private readonly IDictionary<string, ISKFunction> _chatAgent;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;

    public ChatAgent()
    {
        // Create a kernel
        this._chatAgentKernel = KernelUtils.CreateKernel();
        this._actionKernel = KernelUtils.CreateKernel();

        this._chatAgent = this._chatAgentKernel.ImportSkill(this, "ChatAgent");

        string folder = RepoFiles.SampleSkillsPath();
        this._semanticSkills = this._chatAgentKernel.ImportSemanticSkillFromDirectory(folder, "ChatAgentSkill");

        this._chatSkill = this._chatAgentKernel.ImportSkill(new ChatSkill((context) =>
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


        // Create a plan to chat with the user using the chat skill
        this._chatPlan = new Plan("Chat with the user");
        this._chatPlan.Outputs.Add("chat_history");
        var actStep = new Plan(this._chatAgent["ActOnMessage"]);
        actStep.Outputs.Add("chat_history");
        var messageStep = new Plan(this._chatSkill["SendMessage"]);
        messageStep.Outputs.Add("chat_history");
        // this._chatPlan.AddSteps(this._chatAgent["ActOnMessage"], this._chatSkill["SendMessage"]);
        this._chatPlan.AddSteps(actStep, messageStep);

        this.RegisterMessageHandler("ChatAgentSkill");
    }

    public void RegisterMessageHandler(string skillName)
    {
        string folder = RepoFiles.SampleSkillsPath();
        this._actionKernel.ImportSemanticSkillFromDirectory(folder, skillName);
    }

    public void RegisterMessageHandler(object instance, string skillName)
    {
        this._actionKernel.ImportSkill(instance, skillName);
    }

    public async Task<SKContext> RunAsync()
    {
        // Create a context with the condition and the plan
        var context = this._chatAgentKernel.CreateNewContext();
        context.Variables.Update("Chat with the user");
        context.Variables.Set("condition", "User does not say 'goodbye'");
        context.Variables.Set("plan", this._chatPlan.ToJson());

        var agentSkill = new AgentSkill(this._chatAgentKernel);

        var completion = await this._semanticSkills["CreateChat"].InvokeAsync(context);
        context.Variables.Set("message", completion.Result);

        // Invoke the agent skill with the context
        return await agentSkill.RunPlanAsync(context);
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
            var planner = new ActionPlanner(this._actionKernel);
            var plan = await planner.CreatePlanAsync($"Review the most recent 'User:' message and determine which function to run. If unsure, use 'ContinueChat'.\n[MESSAGES]\n{chatHistory}\n[END MESSAGES]\n");

            // var completion = await this._semanticSkills["ContinueChat"].InvokeAsync(context);
            var completion = await plan.InvokeAsync(context);
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
