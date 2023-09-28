// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Orchestration;

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
    public static async Task<ChatHistory> GenerateChatHistoryWithFunctionsAsync(
        this IChatCompletion chatCompletion,
        ChatHistory chat,
        IKernel kernel,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
    {
        ChatHistory returnMessages = chat;
        IReadOnlyFunctionCollection functionCollection = kernel.Functions;
        OpenAIRequestSettings chatRequestSettings = requestSettings as OpenAIRequestSettings ?? new();
        chatRequestSettings.Functions = functionCollection.GetFunctionViews().Select(functionView => functionView.ToOpenAIFunction()).ToList();

        var chatMessages = await chatCompletion.GenerateChatHistoryWithFunctionsAsync(chat, kernel, functionCollection, chatRequestSettings, cancellationToken).ConfigureAwait(false);
        returnMessages.Messages.AddRange(chatMessages.Messages);
        return returnMessages;
    }

    private static async Task<ChatHistory> GenerateChatHistoryWithFunctionsAsync(
        this IChatCompletion chatCompletion,
        ChatHistory chat,
        IKernel kernel,
        IReadOnlyFunctionCollection functionCollection,
        OpenAIRequestSettings chatRequestSettings,
        CancellationToken cancellationToken = default)
    {
        ChatHistory returnMessages = chat;
        var chatResults = await chatCompletion.GetChatCompletionsAsync(chat, chatRequestSettings, cancellationToken).ConfigureAwait(false);
        var firstChatMessage = await chatResults[0].GetChatMessageAsync(cancellationToken).ConfigureAwait(false);
        returnMessages.Messages.Add(firstChatMessage);

        OpenAIFunctionResponse? functionResponse = chatResults[0].GetFunctionResponse();
        if (functionResponse is not null)
        {
            if (functionCollection.TryGetFunction(functionResponse.PluginName, functionResponse.FunctionName, out var function))
            {
                ContextVariables context = new();
                foreach (var parameter in functionResponse.Parameters)
                {
                    // Add parameter to context
                    context.Set(parameter.Key, parameter.Value.ToString());
                }

                var functionResult = await kernel.RunAsync(function, context, cancellationToken).ConfigureAwait(false);
                returnMessages.AddMessage(AuthorRole.Function, functionResult.GetValue<string>() ?? string.Empty);

                var newMesages = await chatCompletion.GenerateChatHistoryWithFunctionsAsync(returnMessages, kernel, functionCollection, chatRequestSettings, cancellationToken).ConfigureAwait(false);
                returnMessages.Messages.AddRange(newMesages.Messages);
            }
        }

        return returnMessages;
    }
}
