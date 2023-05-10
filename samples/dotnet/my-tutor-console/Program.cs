// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;
using Skills;

namespace mytutorconsole;

public static class Program
{
    public static async Task Main()
    {
        #region configure kernel

        Console.OutputEncoding = Encoding.Unicode;

        // Create a kernel
        var kernel = KernelUtils.CreateKernel();

        #endregion

        // Define a ChatAgent and run it
        var chatAgent = new ChatAgent();
        chatAgent.RegisterMessageHandler(new LearningSkill(), "LearningSkill");
        var result = await chatAgent.RunAsync();

        // or Define a StudyAgent and run it -- both conversations right now

        #region comments

        // TODO -- base chat plan that will take a message and either 1) return a response or 2) start a study session agent

        // "What would you like to learn about today?"
        // > "I want to learn about Algebra 1"

        // TODO:
        // 1. (M) CreateLesson --> produce a plan that can be shared rather than a tightly couple chat message thingy
        // 1.5 (M) Save/shared lesson plans
        // 2. (L) Integrate Memory --> use memory to get evaluations, use memory to get context about tutee(s) for lesson creation
        // 2.5 (M) Gather data from file input re: tests/homework with scores
        // 3. (M) Update Plan -> based on memory (where do we save/store plans? memory? file? db?)
        // 4. (S) [Minor] -> update this demo to not be hard coded to Algebra 1.  Maybe use a different skill?

        #endregion

        Console.WriteLine($"{result.Result}");
    }
}
