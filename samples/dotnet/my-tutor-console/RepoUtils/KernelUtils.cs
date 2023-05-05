// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using RepoUtils;

internal static class KernelUtils
{
    internal static IKernel CreateKernel()
    {
        var kernel = new KernelBuilder()
            //.WithLogger(ConsoleLogger.Log)
            .Configure(config =>
            {
                // config.AddAzureTextCompletionService(
                //     Env.Var("AZURE_OPENAI_DEPLOYMENT_NAME"),
                //     Env.Var("AZURE_OPENAI_ENDPOINT"),
                //     Env.Var("AZURE_OPENAI_KEY"));

                config.AddAzureChatCompletionService(
                    Env.Var("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
                    Env.Var("AZURE_OPENAI_CHAT_ENDPOINT"),
                    Env.Var("AZURE_OPENAI_CHAT_KEY"));

                config.AddAzureTextEmbeddingGenerationService(
                    Env.Var("AZURE_OPENAI_EMBEDDINGS_DEPLOYMENT_NAME"),
                    Env.Var("AZURE_OPENAI_EMBEDDINGS_ENDPOINT"),
                    Env.Var("AZURE_OPENAI_EMBEDDINGS_KEY"));
            })
            .WithMemoryStorage(new VolatileMemoryStore())
            .Build();

        return kernel;
    }
}
