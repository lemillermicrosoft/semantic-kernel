// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Planning.IterativePlanner;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planning;
#pragma warning restore IDE0130

public class MrklPlannerChat : MrklPlannerText
{
    private readonly string _systemPromptTemplate;
    private readonly string _userPromptTemplate;

    public MrklPlannerChat(IKernel kernel,
        int maxIterations = 5,
        string? systemPrompt = null,
        string systemResource = "iterative-planer-chat-system.txt",
        string? userPrompt = null,
        string userResource = "iterative-planer-chat-user.txt",
        ILogger? logger = null
    )
        : base(kernel, maxIterations, null, null, logger)
    {
        if (!string.IsNullOrEmpty(systemResource))
        {
            this._systemPromptTemplate = EmbeddedResource.Read(systemResource);
        }

        if (!string.IsNullOrEmpty(userResource))
        {
            this._userPromptTemplate = EmbeddedResource.Read(userResource);
        }

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            this._systemPromptTemplate = systemPrompt;
        }

        if (!string.IsNullOrEmpty(userPrompt))
        {
            this._userPromptTemplate = userPrompt;
        }
    }
}
