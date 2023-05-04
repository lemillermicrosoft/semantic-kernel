// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using RepoUtils;

internal static class KernelUtils
{
    internal static IKernel CreateKernel()
    {
        var kernel = new KernelBuilder() /*.WithLogger(ConsoleLogger.Log)*/.Build();
        // kernel.Config.AddAzureTextCompletionService(
        //     Env.Var("AZURE_OPENAI_DEPLOYMENT_NAME"),
        //     Env.Var("AZURE_OPENAI_ENDPOINT"),
        //     Env.Var("AZURE_OPENAI_KEY"));
        kernel.Config.AddAzureChatCompletionService(
            Env.Var("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
            Env.Var("AZURE_OPENAI_CHAT_ENDPOINT"),
            Env.Var("AZURE_OPENAI_CHAT_KEY"));

        return kernel;
    }
}
