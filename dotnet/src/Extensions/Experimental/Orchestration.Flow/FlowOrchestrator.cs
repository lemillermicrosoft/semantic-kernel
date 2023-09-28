﻿// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Experimental.Orchestration;

/// <summary>
/// A flow orchestrator that using semantic kernel for execution.
/// </summary>
public class FlowOrchestrator
{
    private readonly KernelBuilder _kernelBuilder;

    private readonly IFlowStatusProvider _flowStatusProvider;

    private readonly Dictionary<object, string?> _globalSkillCollection;

    private readonly IFlowValidator _flowValidator;

    private readonly FlowOrchestratorConfig? _config;

    /// <summary>
    /// Initialize a new instance of the <see cref="FlowOrchestrator"/> class.
    /// </summary>
    /// <param name="kernelBuilder">The semantic kernel builder.</param>
    /// <param name="flowStatusProvider">The flow status provider.</param>
    /// <param name="globalSkillCollection">The global skill collection</param>
    /// <param name="validator">The flow validator.</param>
    /// <param name="config">Optional configuration object</param>
    public FlowOrchestrator(
        KernelBuilder kernelBuilder,
        IFlowStatusProvider flowStatusProvider,
        Dictionary<object, string?>? globalSkillCollection = null,
        IFlowValidator? validator = null,
        FlowOrchestratorConfig? config = null)
    {
        Verify.NotNull(kernelBuilder);

        this._kernelBuilder = kernelBuilder;
        this._flowStatusProvider = flowStatusProvider;
        this._globalSkillCollection = globalSkillCollection ?? new Dictionary<object, string?>();
        this._flowValidator = validator ?? new FlowValidator();
        this._config = config;
    }

    /// <summary>
    /// Execute a given flow.
    /// </summary>
    /// <param name="flow">goal to achieve</param>
    /// <param name="sessionId">execution session id</param>
    /// <param name="input">current input</param>
    /// <param name="contextVariables">execution context variables</param>
    /// <returns>SKContext, which includes a json array of strings as output. The flow result is also exposed through the context when completes.</returns>
    public async Task<ContextVariables> ExecuteFlowAsync(
        [Description("The flow to execute")] Flow flow,
        [Description("Execution session id")] string sessionId,
        [Description("Current input")] string input,
        [Description("Execution context variables")]
        ContextVariables? contextVariables = null)
    {
        try
        {
            this._flowValidator.Validate(flow);
        }
        catch (Exception ex)
        {
            throw new SKException("Invalid flow", ex);
        }

        FlowExecutor executor = new(this._kernelBuilder, this._flowStatusProvider, this._globalSkillCollection, this._config);
        return await executor.ExecuteAsync(flow, sessionId, input, contextVariables ?? new ContextVariables(null)).ConfigureAwait(false);
    }
}
