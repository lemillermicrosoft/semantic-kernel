// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Reliability;
using Reliability;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example08_RetryPolicy
{
    public static async Task RunAsync()
    {
        IHttpRetryPolicy retryMechanism = new RetryThreeTimesWithBackoff();
        Console.WriteLine("============================ RetryThreeTimesWithBackoff ============================");
        await RunRetryPolicyAsync(retryMechanism);

        retryMechanism = new RetryThreeTimesWithRetryAfterBackoff();
        Console.WriteLine("============================ RetryThreeTimesWithRetryAfterBackoff ============================");
        await RunRetryPolicyAsync(retryMechanism);

        var defaultConfigPlusUnauthorized = new Microsoft.SemanticKernel.Configuration.KernelConfig.HttpRetryConfig();

        // Add 401 to the list of retryable status codes
        // Typically 401 would not be something we retry but for demonstration
        // purposes we are doing so as it's easy to trigger when using an invalid key.
        defaultConfigPlusUnauthorized.RetryableStatusCodes.Add(System.Net.HttpStatusCode.Unauthorized);
        retryMechanism = new DefaultHttpRetryPolicy(defaultConfigPlusUnauthorized);
        Console.WriteLine("============================ DefaultHttpRetryPolicy ============================");
        await RunRetryPolicyAsync(retryMechanism);

        retryMechanism = new NullRetryPolicy();
        Console.WriteLine("============================ NoRetryPolicy ============================");
        await RunRetryPolicyAsync(retryMechanism);

        var config = new Microsoft.SemanticKernel.Configuration.KernelConfig.HttpRetryConfig() { MaxRetryCount = 3, UseExponentialBackoff = true };

        // Add 401 to the list of retryable status codes
        // Typically 401 would not be something we retry but for demonstration
        // purposes we are doing so as it's easy to trigger when using an invalid key.
        config.RetryableStatusCodes.Add(System.Net.HttpStatusCode.Unauthorized);

        retryMechanism = new DefaultHttpRetryPolicy(config);
        Console.WriteLine(
            "============================ DefaultHttpRetryPolicy with MaxRetryCount = 3 and UseExponentialBackoff = true ============================");
        await RunRetryPolicyAsync(retryMechanism);
    }

    private static async Task RunRetryPolicyAsync(IHttpRetryPolicy retryMechanism)
    {
        IKernel kernel = Kernel.Builder.WithLogger(ConsoleLogger.Log).Build();
        kernel.Config.SetHttpRetryPolicy(retryMechanism);

        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernel.Config.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY");

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
