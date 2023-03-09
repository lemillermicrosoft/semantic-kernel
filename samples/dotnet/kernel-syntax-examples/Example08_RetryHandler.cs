// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Configuration;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Reliability;
using Reliability;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example08_RetryHandler
{
    public static async Task RunAsync()
    {
        var kernel = InitializeKernel();
        var retryHandlerFactory = new RetryThreeTimesWithBackoffFactory(kernel.Log);
        Console.WriteLine("============================== RetryThreeTimesWithBackoff ==============================");
        await RunRetryPolicyAsync(kernel, retryHandlerFactory);

        var retryHandlerFactory2 = new RetryThreeTimesWithRetryAfterBackoffFactory(kernel.Log);
        Console.WriteLine("========================= RetryThreeTimesWithRetryAfterBackoff =========================");
        await RunRetryPolicyAsync(kernel, retryHandlerFactory2);

        Console.WriteLine("==================================== NoRetryPolicy =====================================");
        await RunRetryPolicyAsync(kernel, new NullHttpRetryHandlerFactory());

        Console.WriteLine("=============================== DefaultHttpRetryHandler ================================");
        await RunRetryHandlerConfigAsync(new KernelConfig.HttpRetryConfig() { MaxRetryCount = 3, UseExponentialBackoff = true });

        Console.WriteLine("======= DefaultHttpRetryConfig [MaxRetryCount = 3, UseExponentialBackoff = true] ====== ");
        await RunRetryHandlerConfigAsync(new KernelConfig.HttpRetryConfig() { MaxRetryCount = 3, UseExponentialBackoff = true });
    }

    private static async Task RunRetryHandlerConfigAsync(KernelConfig.HttpRetryConfig? config = null)
    {
        var kernelBuilder = Kernel.Builder.WithLogger(ConsoleLogger.Log);
        if (config != null)
        {
            kernelBuilder = kernelBuilder.Configure(c => c.SetDefaultHttpRetryConfig(config));
        }

        // Add 401 to the list of retryable status codes
        // Typically 401 would not be something we retry but for demonstration
        // purposes we are doing so as it's easy to trigger when using an invalid key.
        kernelBuilder = kernelBuilder.Configure(c => c.DefaultHttpRetryConfig.RetryableStatusCodes.Add(System.Net.HttpStatusCode.Unauthorized));

        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernelBuilder = kernelBuilder.Configure(c => c.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY"));

        var kernel = kernelBuilder.Build();

        await ImportAndExecuteSkillAsync(kernel);
    }

    private static IKernel InitializeKernel()
    {
        var kernel = Kernel.Builder.WithLogger(ConsoleLogger.Log).Build();
        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernel.Config.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY");

        return kernel;
    }

    private static async Task RunRetryPolicyAsync(IKernel kernel, IDelegatingHandlerFactory retryHandlerFactory)
    {
        kernel.Config.SetHttpHandlerFactory(retryHandlerFactory);
        await ImportAndExecuteSkillAsync(kernel);
    }

    private static async Task ImportAndExecuteSkillAsync(IKernel kernel)
    {
        // Load semantic skill defined with prompt templates
        string folder = RepoFiles.SampleSkillsPath();

        kernel.ImportSkill(new TimeSkill(), "time");

        var qaSkill = kernel.ImportSemanticSkillFromDirectory(
            folder,
            "QASkill");

        var question = "How popular is Polly library?";

        // To see the retry policy in play, you can set the OPENAI_API_KEY to an invalid value
        var answer = await kernel.RunAsync(question, qaSkill["Question"]);

        Console.WriteLine($"Question: {question}\n\n" + answer);
    }
}

/* Output:
============================== RetryThreeTimesWithBackoff ==============================
warn: object[0]
      Error executing action [attempt 1 of 3], pausing 2000 msecs. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 2 of 3], pausing 4000 msecs. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 3 of 3], pausing 8000 msecs. Outcome: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
Question: How popular is Polly library?

Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
========================= RetryThreeTimesWithRetryAfterBackoff =========================
warn: object[0]
      Error executing action [attempt 1 of 3], pausing 2000 msecs. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 2 of 3], pausing 2000 msecs. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 3 of 3], pausing 2000 msecs. Outcome: Unauthorized
Question: How popular is Polly library?

Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
=============================== DefaultHttpRetryHandler ================================
warn: object[0]
      Error executing action [attempt 1 of 1]. Reason: Unauthorized. Will retry after 2000ms
warn: object[0]
      Error executing request, max retry count reached. Reason: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
Question: How popular is Polly library?

Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
==================================== NoRetryPolicy =====================================
Question: How popular is Polly library?

Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
======= DefaultHttpRetryConfig [MaxRetryCount = 3, UseExponentialBackoff = true] ======
warn: object[0]
      Error executing action [attempt 1 of 3]. Reason: Unauthorized. Will retry after 2000ms
warn: object[0]
      Error executing action [attempt 2 of 3]. Reason: Unauthorized. Will retry after 4000ms
warn: object[0]
      Error executing action [attempt 3 of 3]. Reason: Unauthorized. Will retry after 8000ms
warn: object[0]
      Error executing request, max retry count reached. Reason: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
Question: How popular is Polly library?

Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
== DONE ==
*/
