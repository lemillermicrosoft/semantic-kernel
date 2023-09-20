// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.SemanticKernel.AI.ChatCompletion;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for the IChatCompletion interface.
/// </summary>
public static class ChatCompletionExtensions
{
    /// <summary>
    /// Generates a new chat message asynchronously using Functions.
    /// </summary>
    /// <param name="chatCompletion">The target IChatCompletion interface to extend.</param>
    /// <param name="chat">The chat history.</param>
    /// <param name="kernel">The kernel to use for function execution.</param>
    /// <param name="requestSettings">The AI request settings (optional).</param>
    /// <param name="cancellationToken">The asynchronous cancellation token (optional).</param>
    /// <remarks>This extension does not support multiple prompt results (only the first will be returned).</remarks>
    /// <returns>A task representing the generated chat message in string format.</returns>
    public static async Task<string> GenerateMessageWithFunctionsAsync(
        this IChatCompletion chatCompletion,
        ChatHistory chat,
        IKernel kernel,
        ChatRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySkillCollection skillCollection = kernel.Skills;
        OpenAIChatRequestSettings chatRequestSettings = requestSettings as OpenAIChatRequestSettings ?? new();
        chatRequestSettings.Functions = skillCollection.GetFunctionsView().ToOpenAIFunctions();

        var chatResults = await chatCompletion.GetChatCompletionsAsync(chat, chatRequestSettings, cancellationToken).ConfigureAwait(false);
        var firstChatMessage = await chatResults[0].GetChatMessageAsync(cancellationToken).ConfigureAwait(false);

        OpenAIFunctionResult? functionResult = chatResults[0].GetFunctionResult();
        if (functionResult is not null)
        {
            if (skillCollection.TryGetFunction(functionResult.PluginName, functionResult.FunctionName, out var function))
            {
                ContextVariables context = new();
                foreach (var parameter in functionResult.Parameters)
                {
                    // Add parameter to context
                    context.Set(parameter.Key, parameter.Value.ToString());
                }

                return (await kernel.RunAsync(function, context, cancellationToken).ConfigureAwait(false)).Result;
            }
        }

        return firstChatMessage.Content;
    }
}
