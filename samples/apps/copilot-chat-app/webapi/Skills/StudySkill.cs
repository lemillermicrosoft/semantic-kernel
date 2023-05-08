// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SemanticKernel.Service.Skills;

public class StudySkill
{
    private readonly IKernel _studySkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _chatSkill;
    private readonly IDictionary<string, ISKFunction> _studySkill;

    public StudySkill(IKernel kernel)
    {
        #region create a kernel

        // Create a kernel
        this._studySkillKernel = kernel;//KernelUtils.CreateKernel();

        #endregion

        // string folder = RepoFiles.SampleSkillsPath();
        // this._semanticSkills = this._studySkillKernel.ImportSemanticSkillFromDirectory(folder,
        //     "StudySkill",
        //     "DoWhileSkill");
        this._semanticSkills = kernel.RegisterNamedSemanticSkills(null, null, "StudySkill", "DoWhileSkill");

        this._studySkill = this._studySkillKernel.ImportSkill(this, "StudySkill");

        this._doWhileSkill = this._studySkillKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");

        // this._chatSkill = this._studySkillKernel.ImportSkill(new ChatSkill((context) =>
        // {
        //     var line = $"Study Agent: {context.Variables.Input.Trim()}";
        //     Console.WriteLine(line);
        //     context.Variables.Update(line);
        //     context.Variables.Get("chat_history", out var chatHistory);
        //     context.Variables.Set("chat_history", $"{chatHistory}\n{line}");
        //     Console.Write("User: ");
        //     return Task.FromResult(context);
        // }, (context) =>
        // {
        //     var line = Console.ReadLine();
        //     context.Variables.Update($"User: {line}");
        //     context.Variables.Get("chat_history", out var chatHistory);
        //     context.Variables.Set("chat_history", $"{chatHistory}\nUser: {line}");
        //     return Task.FromResult(context);
        // }), "ChatSkill");
    }

    // StudySession
    [SKFunction(description: "Study session")]
    [SKFunctionName("StudySession")]
    [SKFunctionContextParameter(Name = "topic", Description = "Topic to study e.g. 'Equations and inequalities'")]
    [SKFunctionContextParameter(Name = "course", Description = "The course of study e.g. 'Algebra 1'")]
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

        var createLessonContext = this._studySkillKernel.CreateNewContext();
        foreach (KeyValuePair<string, string> x in studySessionContext)
        {
            createLessonContext.Variables[x.Key] = x.Value;
        }

        var lessonStart = await lessonFunction.InvokeAsync(createLessonContext);

        //
        // Chat with the user about the lesson until they say goodbye
        //
        var plan = new Plan("Prepare a message and send it.");
        plan.Outputs.Add("course");
        plan.Outputs.Add("topic");
        plan.Outputs.Add("context");
        var prepareStep = new Plan(this._studySkill["PrepareMessage"]);
        prepareStep.Outputs.Add("chat_history");
        var sendStep = new Plan(this._chatSkill["SendMessage"]);
        sendStep.Outputs.Add("chat_history");
        plan.AddSteps(prepareStep, sendStep);

        var doWhileContext = new ContextVariables(lessonStart.Result);
        doWhileContext.Set("message", lessonStart.Result);
        doWhileContext.Set("topic", topic);
        if (context.Variables.Get("course", out var course))
        {
            doWhileContext.Set("course", course);
        }

        doWhileContext.Set("action", plan.ToJson());
        doWhileContext.Set("condition", "User does not say 'goodbye'"); // todo advanced condition like amount of time, etc.

        return await this._studySkillKernel.RunAsync(doWhileContext, this._doWhileSkill["DoWhile"]);
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
