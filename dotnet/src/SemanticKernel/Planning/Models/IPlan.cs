// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning.Models;

public interface IPlan
{
    // Current properties on a Plan
    // string

    // bool IsComplete

    // bool IsSuccessful

    // string Result

    // Also exists today on Plan
    string Goal { get; }

    // Today, the result of calling Create|Execute plan is SKContext with ContextVariables that contain both the state and the plan itself.
    // In future, methods or extensions for RunPlan will instead return the plan, with the context place inside of it. Need to understand the why better.
    ContextVariables State { get; } // TODO WorkingMemory instead?

    Task<IPlan> RunNextStepAsync(IKernel kernel, ContextVariables variables, CancellationToken cancellationToken = default);
}
