// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using RepoUtils;
using Skills;

// Learning objectives: This refers to what you want to learn or achieve by the end of the learning experience. The objectives should be specific, measurable, achievable, relevant, and time-bound.

// Assessment: This involves identifying your current knowledge or skills level to determine where you need to improve or learn more. Assessment can be done through self-assessment, observation, or formal testing.

// Learning activities: This includes the specific tasks or activities that will help you achieve your learning objectives. These may include reading, attending lectures or workshops, completing assignments or projects, practicing skills, or participating in discussions.

// Resources: This refers to the tools, materials, and support you need to complete the learning activities. Resources may include textbooks, online resources, software, mentors, or peer support.

// Timeline: This outlines the specific deadlines or milestones for completing the learning activities and achieving the learning objectives.

// Evaluation: This involves assessing your progress and evaluating the effectiveness of the learning plan in achieving your goals. Evaluation can be done through self-reflection, feedback from others, or formal assessment.
namespace mytutorconsole;

// public class LearningPlan
// {
//     public string Subject { get; set; } // e.g. "9th grade Algebra"
//     public string Prompt { get; set; } // e.g. "Help me learn about: '{subject}'\n"
//     public string[] LearningObjectives { get; set; } // e.g. "Learn how to solve linear equations"
//     public string[] LearningActivities { get; set; } // e.g. "Read chapter 1 of textbook"
//     public string[] Resources { get; set; } // e.g. "Textbook: Algebra 1"
//     public string[] Timeline { get; set; } // e.g. "Complete chapter 1 by 9/1/2021"
//     public string[] Evaluation { get; set; } // e.g. "Take quiz on chapter 1"
// }

public static class Program
{
    // ReSharper disable once InconsistentNaming
    public static async Task Main()
    {
        // Create a kernel
        var kernel = new KernelBuilder() /*.WithLogger(ConsoleLogger.Log)*/.Build();
        kernel.Config.AddAzureChatCompletionService(
            Env.Var("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
            Env.Var("AZURE_OPENAI_CHAT_ENDPOINT"),
            Env.Var("AZURE_OPENAI_CHAT_KEY"));

        string folder = RepoFiles.SampleSkillsPath();
        var skills = kernel.ImportSemanticSkillFromDirectory(folder, "StudySkill");
        var studySKill = kernel.ImportSkill(new StudySkill(), "StudySkill");

        // var plan = await kernel.CreatePlanAsync("Help me study for my test on Algebra.");
        var plan = new Plan("Help me study for my test on Algebra 1.");
        plan.State.Set("course", "Algebra 1");
        plan.State.Set("context", "No Context Available");
        plan.AddSteps(skills["CreateLessonTopics"], skills["SelectLessonTopic"], studySKill["StudySession"]); // todo foreach handling

        var result = await plan.InvokeAsync(kernel.CreateNewContext());
        Console.WriteLine($"Result: {result.Result}");
    }

    private static string PlanToString(Plan originalPlan, string indent = " ")
    {
        string goalHeader = $"{indent}Goal: {originalPlan.Description}\n\n{indent}Steps:\n";

        string stepItems = string.Join("\n", originalPlan.Steps.Select(step =>
        {
            if (step.Steps.Count == 0)
            {
                string skillName = step.SkillName;
                string stepName = step.Name;

                string namedParams = string.Join(" ", step.Parameters.Select(param => $"{param.Key}='{param.Value}'"));
                if (!string.IsNullOrEmpty(namedParams))
                {
                    namedParams = $" {namedParams}";
                }

                string namedOutputs = step.Outputs.FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrEmpty(namedOutputs))
                {
                    namedOutputs = $" => {namedOutputs}";
                }

                return $"{indent}{indent}- {string.Join(".", skillName, stepName)}{namedParams}{namedOutputs}";
            }
            else
            {
                string nestedSteps = PlanToString(step, indent + indent);
                return nestedSteps;
            }
        }));

        return goalHeader + stepItems;
    }
}

// string folder = RepoFiles.SampleSkillsPath();
// var skills = kernel.ImportSemanticSkillFromDirectory(folder,
//     "StudySkill",
//     "DoWhileSkill");

// Console.WriteLine("ActionPlanner plan:");
// try
// {
//     var actionPlanner = new ActionPlanner(kernel);
//     var actionPlan = await actionPlanner.CreatePlanAsync("Write a poem about John Doe, then translate it into Italian.");

//     Console.WriteLine(PlanToString(actionPlan));
// }
// catch (PlanningException e)
// {
//     Console.WriteLine("Could not create Action Plan");
// }

// Create learning plan
// Learn/remember about user (or their class).  What are their goals?  What are their interests?  What are their strengths and weaknesses?

// Retrieve current lesson/quiz/references

// Begin new tutor session:
// Prompt user for which subject if not present in context
// Evaluate current knowledge of subject

// Given evaluation, prepare learning plan ==> STATIC now
// - Identify skills or topics to learn/review/practice/test
// - For each skill or topic, generate [or identify] learning content (e.g. video, article, quiz, etc.)
// - Add learning content steps to learning plan
// - Add evaluation steps to learning plan (e.g. quiz, test, etc.)

// Console.WriteLine("SequentialPlanner plan:");
// try
// {
//     var subject = "9th grade Algebra";
//     var prompt = $"Help me learn about: '{subject}'\n";

//     var config = new SequentialPlannerConfig();
//     config.ExcludedSkills.Add("this");
//     var planner = new SequentialPlanner(kernel, config);
//     var plan = await planner.CreatePlanAsync("Help me learn 9th grade Algebra.");

//     Console.WriteLine(PlanToString(plan));
// }
// catch (PlanningException e)
// {
//     Console.WriteLine("Could not create Sequential Plan");
// }
// string firstUserMessage = "Hello!";
// kernel.ImportSkill(new ChatSkill((context) =>
// {
//     var line = $"Bot: {context.Variables.Input.Trim()}";
//     Console.WriteLine(line);
//     context.Variables.Update(line);
//     context.Variables.Get("chat_history", out var chatHistory);
//     context.Variables.Set("chat_history", $"{chatHistory}\n{line}");
//     Console.Write("User: ");
//     return Task.FromResult<SKContext>(context);
// }, (context) =>
//     {
//         // var line = string.IsNullOrEmpty(firstUserMessage) ? Console.ReadLine() : firstUserMessage;
//         // firstUserMessage = null;
//         var line = Console.ReadLine();
//         context.Variables.Update($"User: {line}");
//         context.Variables.Get("chat_history", out var chatHistory);
//         context.Variables.Set("chat_history", $"{chatHistory}\nUser: {line}");
//         return Task.FromResult<SKContext>(context);
//     }), "ChatSkill");

// var dws = kernel.ImportSkill(new DoWhileSkill(skills["IsTrue"]), "DoWhileSkill");

// var context = new ContextVariables("Hello, I'm a bot!");
// context.Set("action", "ChatSkill.SendMessage"); // AND THEN WAIT FOR MESSAGE BEFORE EVALUATING CONDITION
// // Instead of passing in `skills["IsTrue"] -- we can pass in a plan object!
// context.Set("condition", "User does not say 'goodbye'");
// await kernel.RunAsync(context, dws["DoWhile"]);
