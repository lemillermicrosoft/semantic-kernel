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
            "What is the capital of France? Who is that cities current mayor? What percentage of their life has been in the 21st century as of today?",
        };

        // Result :Leo DiCaprio's girlfriend is Camila Morrone, and her current age raised to the 0.43 power is about 4.06.
        // Result :The current president of the United States is Joe Biden and his current age divided by 2 is 39.
        // Result :The capital of France is Paris. The current mayor of Paris is Anne Hidalgo. 33.87% of her life has been in the 21st century as of today.

        foreach (var goal in goals)
        {
            Console.WriteLine("*****************************************************");
            Console.WriteLine("Goal :" + goal);
            Console.WriteLine("*****************************************************");

            var config = new Microsoft.SemanticKernel.Planning.MrklSystem.MrklSystemPlannerConfig();
            config.ExcludedFunctions.Add("TranslateMathProblem");

            MrklSystemPlanner planner = new(kernel, config);
            var plan = planner.CreatePlan(goal);

            var result = await plan.InvokeAsync(kernel.CreateNewContext());
            Console.WriteLine("*****************************************************");
            Console.WriteLine("Result :" + result);
            if (result.Variables.Get("stepCount", out var stepCount))
            {
                Console.WriteLine("Steps Taken: " + stepCount);
            }
            if (result.Variables.Get("skillCount", out var skillCount))
            {
                Console.WriteLine("Skills Used: " + skillCount);
            }
            Console.WriteLine("*****************************************************");
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

// *****************************************************
// Goal :Who is Leo DiCaprio's girlfriend? What is her current age raised to the 0.43 power?
// *****************************************************
// 01:54:27 Observation : Sarah Stier // Getty Images March 2021: They enjoy a beachside getaway. DiCaprio and Morrone headed to Malibu with friends for brief holiday. The actress shared photos from their trip to...
// 01:54:32 Observation : Michele Morrone ( Italian pronunciation: [mi'k??le mor'ro?ne]; born 3 October 1990) is an Italian actor, model, singer, and fashion designer appearing in both Italian and Polish films. He gained international recognition after portraying the role of Massimo Torricelli in the 2020 erotic romantic drama 365 Days . Early life
// 01:54:37 Observation : Camila Rebeca Morrone Polak [3] (born June 16, 1997) [4] is an American model and actress. She made her acting debut in the James Franco film Bukowski and subsequently appeared in the films Death Wish and Never Goin' Back, both premiering at the Sundance Film Festival in January 2018. Early life and education
// 01:54:44 Observation : Answer:3.9218486893172186
// *****************************************************
// Result :Leo DiCaprio's girlfriend is Camila Morrone and her current age raised to the 0.43 power is 3.92.
// Steps Taken: 5
// *****************************************************
// *****************************************************
// Goal :Who is the current president of the United States? What is his current age divided by 2
// *****************************************************
// 01:54:52 Observation : U.S. facts and figures Presidents, vice presidents, and first ladies Presidents, vice presidents, and first ladies Learn about the duties of president, vice president, and first lady of the United States. Find out how to contact and learn more about current and past leaders. President of the United States Vice president of the United States
// 01:55:00 Observation : On January 20, 2021, at the age of 78, Biden became the oldest president in U.S. history, the first to have a female vice president, and the first from Delaware. ... : 411-414, 419 Obama campaign staffers called Biden's blunders "Joe bombs" and kept Biden uninformed about strategy discussions, which in turn irked Biden. Relations between the ...
// 01:55:04 Observation : Answer:39
// *****************************************************
// Result :The current president of the United States is Joe Biden, and his current age divided by 2 is 39.
// Steps Taken: 4
// *****************************************************
// *****************************************************
// Goal :What is the capital of France? Who is that cities current mayor? What percentage of their life has been in the 21st century as of today?
// *****************************************************
// 01:55:13 Observation : Paris is the capital of what country? Paris; Eiffel Tower
// 01:55:24 Observation : Early life and education [ edit] Family background and youth [ edit] Hidalgo was born in San Fernando, province of Cádiz, Spain. [1] Her paternal grandfather was a Spanish Socialist who became a refugee in France after the end of the Spanish Civil War along with his wife and his four children.
// 01:55:28 Observation : Friday, June 2, 2023 1:55 PM
// 01:55:41 Observation : Answer:35.9375
// *****************************************************
// Result :The capital of France is Paris. The current mayor of Paris is Anne Hidalgo. As of 2023, 35.9375 percent of her life has been in the 21st century.
// Steps Taken: 8
// *****************************************************
// == DONE ==
