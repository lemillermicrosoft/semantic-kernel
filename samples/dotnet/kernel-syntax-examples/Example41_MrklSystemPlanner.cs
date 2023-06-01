// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using NCalcSkills;
using RepoUtils;

/**
 * This example shows how to use MRKL System Planner to create a plan for a given goal.
 */

// ReSharper disable once InconsistentNaming
public static class Example41_MrklSystemPlanner
{
    public static async Task RunAsync()
    {
        var kernel = GetKernel();

        using var bingConnector = new BingConnector(Env.Var("BING_API_KEY"));

        var webSearchEngineSkill = new WebSearchEngineSkill(bingConnector);

        kernel.ImportSkill(webSearchEngineSkill, "WebSearch");

        kernel.ImportSkill(new LanguageCalculatorSkill(kernel), "calculator");

        kernel.ImportSkill(new TimeSkill(), "time");

        string[] goals = new string[] {
            "Who is Leo DiCaprio's girlfriend? What is her current age raised to the 0.43 power?",
            "Who is the current president of the United States? What is his current age divided by 2",
        };

        // Result :Leo DiCaprio's girlfriend is Camila Morrone, and her current age raised to the 0.43 power is about 4.06.
        // Result :The current president of the United States is Joe Biden and his current age divided by 2 is 39.

        foreach (var goal in goals)
        {
            MrklSystemPlanner planner = new(kernel);
            var plan = planner.CreatePlan(goal);

            var result = await plan.InvokeAsync(kernel.CreateNewContext());

            Console.WriteLine("Result :" + result);
        }
    }

    private static IKernel GetKernel()
    {
        var kernel = new KernelBuilder()
        .WithAzureTextCompletionService(
            Env.Var("AZURE_OPENAI_DEPLOYMENT_NAME"),
            Env.Var("AZURE_OPENAI_ENDPOINT"),
            Env.Var("AZURE_OPENAI_KEY"))
            .WithLogger(ConsoleLogger.Log)
            .Configure(c => c.SetDefaultHttpRetryConfig(new HttpRetryConfig
            {
                MaxRetryCount = 3,
                UseExponentialBackoff = true,
                MinRetryDelay = TimeSpan.FromSeconds(3),
                //  MaxRetryDelay = TimeSpan.FromSeconds(8),
            }))
            .Build();

        return kernel;
    }
}
