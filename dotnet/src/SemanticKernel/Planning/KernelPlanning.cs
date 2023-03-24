// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning;

// public class KernelExtensions
// {
//     // TODO - is the plan the actual Object or is this of type ISKFunction[]?
//     // TODO - Experiment with the plan having a cast to ISKFunction[] and see if it works
//     Task<SKContext> RunAsync(IPlan plan)
//     {
//         throw new NotImplementedException();
//     }

//     Task<SKContext> RunAsync(
//         IPlan plan,
//         CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//     }

//     Task<SKContext> RunAsync(
//         string input,
//         IPlan plan)
//     {
//         throw new NotImplementedException();
//     }

//     Task<SKContext> RunAsync(
//         string input,
//         IPlan plan,
//         CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//     }

//     Task<SKContext> RunAsync(
//         ContextVariables variables,
//         IPlan plan)
//     {
//         throw new NotImplementedException();
//     }

//     Task<SKContext> RunAsync(
//         ContextVariables variables,
//         IPlan plan,
//         CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//     }
// }


public static class KernelPlanningExtensions
{
    // TODO - is the plan the actual Object or is this of type ISKFunction[]?
    // TODO - Experiment with the plan having a cast to ISKFunction[] and see if it works
    private static Task<SKContext> RunAsync(this IKernel kernel, IPlan plan)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        string input,
        IPlan plan)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        string input,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        ContextVariables variables,
        IPlan plan)
    {
        throw new NotImplementedException();
    }

    private static Task<SKContext> RunAsync(
        this IKernel kernel,
        ContextVariables variables,
        IPlan plan,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

