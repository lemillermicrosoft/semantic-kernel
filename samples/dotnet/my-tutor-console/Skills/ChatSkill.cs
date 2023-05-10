// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Skills;

// Chat With User until they say Goodbye (Say hello, wait for response, respond, repeat, exit on goodbye)
// |
// +-- ChatSkill.SendMessage("Hello")
// |   |
// |   +-- ChatSkill.WaitForResponse()
// |   |   |
// |   |   +-- ChatSkill.SendMessage("How are you?")
// |   |   |   |
// |   |   |   +-- ChatSkill.WaitForResponse()
// |   |   |   |   |
// |   |   |   |   +-- ChatSkill.SendMessage("Goodbye")

// ChatSkill -- a skill that can be used to chat with the user particularly in conjunction with a plan.
public class ChatSkill
{
    public ChatSkill(
        Func<SKContext, Task<SKContext>>? sendMessage, // callback for SendMessage events
        Func<SKContext, Task<SKContext>>? waitForResponse // callback for WaitForResponse events
    )
    {
        this.sendMessage = sendMessage ?? (context => Task.FromResult(context));
        this.waitForResponse = waitForResponse ?? (context => Task.FromResult(context));
    }

    // Send Message
    [SKFunction(description: "Send message")]
    [SKFunctionName("SendMessage")]
    [SKFunctionContextParameter(Name = "input", Description = "Message to send")]
    public async Task<SKContext> SendMessageAsync(SKContext context)
    {
        context = await this.sendMessage(context); // this sets "chat_history" in the context

        context = await this.WaitForResponseAsync(context); // this also sets "chat_history" in the context

        return context;
    }

    // Wait for response
    [SKFunction(description: "Wait for response")]
    [SKFunctionName("WaitForResponse")]
    public async Task<SKContext> WaitForResponseAsync(SKContext context)
    {
        context = await this.waitForResponse(context);

        // DO SOMETHING WITH THE RESPONSE
        return context;
    }

    // Execute action
    [SKFunction(description: "Execute action")]
    public Task<SKContext> ExecuteAction(SKContext context)
    {
        return Task.FromResult(context);
    }

    // end conversation
    [SKFunction(description: "End conversation")]
    public Task<SKContext> EndConversation(SKContext context)
    {
        return Task.FromResult(context);
    }

    // get conversation history
    [SKFunction(description: "Get conversation history")]
    public Task<SKContext> GetConversationHistory(SKContext context)
    {
        return Task.FromResult(context);
    }

    private Func<SKContext, Task<SKContext>> sendMessage { get; }
    private Func<SKContext, Task<SKContext>> waitForResponse { get; }
}
