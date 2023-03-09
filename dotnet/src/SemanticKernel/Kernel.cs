﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.OpenAI.Services;
using Microsoft.SemanticKernel.Configuration;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Semantic kernel class.
/// The kernel provides a skill collection to define native and semantic functions, an orchestrator to execute a list of functions.
/// Semantic functions are automatically rendered and executed using an internal prompt template rendering engine.
/// Future versions will allow to:
/// * customize the rendering engine
/// * include branching logic in the functions pipeline
/// * persist execution state for long running pipelines
/// * distribute pipelines over a network
/// * RPC functions and secure environments, e.g. sandboxing and credentials management
/// * auto-generate pipelines given a higher level goal
/// </summary>
public sealed class Kernel : IKernel, IDisposable
{
    /// <inheritdoc/>
    public KernelConfig Config => this._config;

    /// <inheritdoc/>
    public ILogger Log => this._log;

    /// <inheritdoc/>
    public ISemanticTextMemory Memory => this._memory;

    /// <inheritdoc/>
    public IReadOnlySkillCollection Skills => this._skillCollection.ReadOnlySkillCollection;

    /// <inheritdoc/>
    public IPromptTemplateEngine PromptTemplateEngine => this._promptTemplateEngine;

    /// <summary>
    /// Return a new instance of the kernel builder, used to build and configure kernel instances.
    /// </summary>
    public static KernelBuilder Builder => new();

    /// <summary>
    /// Kernel constructor. See KernelBuilder for an easier and less error prone approach to create kernel instances.
    /// </summary>
    /// <param name="skillCollection"></param>
    /// <param name="promptTemplateEngine"></param>
    /// <param name="memory"></param>
    /// <param name="config"></param>
    /// <param name="log"></param>
    public Kernel(
        ISkillCollection skillCollection,
        IPromptTemplateEngine promptTemplateEngine,
        ISemanticTextMemory memory,
        KernelConfig config,
        ILogger log)
    {
        this._log = log;
        this._config = config;
        this._memory = memory;
        this._promptTemplateEngine = promptTemplateEngine;
        this._skillCollection = skillCollection;
    }

    /// <inheritdoc/>
    public ISKFunction RegisterSemanticFunction(string functionName, SemanticFunctionConfig functionConfig)
    {
        return this.RegisterSemanticFunction(SkillCollection.GlobalSkill, functionName, functionConfig);
    }

    /// <inheritdoc/>
    public ISKFunction RegisterSemanticFunction(string skillName, string functionName, SemanticFunctionConfig functionConfig)
    {
        // Future-proofing the name not to contain special chars
        Verify.ValidSkillName(skillName);
        Verify.ValidFunctionName(functionName);

        ISKFunction function = this.CreateSemanticFunction(skillName, functionName, functionConfig);
        this._skillCollection.AddSemanticFunction(function);

        return function;
    }

    /// <inheritdoc/>
    public IDictionary<string, ISKFunction> ImportSkill(object skillInstance, string skillName = "")
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            skillName = SkillCollection.GlobalSkill;
            this._log.LogTrace("Importing skill {0} in the global namespace", skillInstance.GetType().FullName);
        }
        else
        {
            this._log.LogTrace("Importing skill {0}", skillName);
        }

        var skill = new Dictionary<string, ISKFunction>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<ISKFunction> functions = ImportSkill(skillInstance, skillName, this._log);
        foreach (ISKFunction f in functions)
        {
            f.SetDefaultSkillCollection(this.Skills);
            this._skillCollection.AddNativeFunction(f);
            skill.Add(f.Name, f);
        }

        return skill;
    }

    /// <inheritdoc/>
    public void RegisterMemory(ISemanticTextMemory memory)
    {
        this._memory = memory;
    }

    /// <inheritdoc/>
    public Task<SKContext> RunAsync(params ISKFunction[] pipeline)
        => this.RunAsync(new ContextVariables(), pipeline);

    /// <inheritdoc/>
    public Task<SKContext> RunAsync(string input, params ISKFunction[] pipeline)
        => this.RunAsync(new ContextVariables(input), pipeline);

    /// <inheritdoc/>
    public Task<SKContext> RunAsync(ContextVariables variables, params ISKFunction[] pipeline)
        => this.RunAsync(variables, CancellationToken.None, pipeline);

    /// <inheritdoc/>
    public Task<SKContext> RunAsync(CancellationToken cancellationToken, params ISKFunction[] pipeline)
        => this.RunAsync(new ContextVariables(), cancellationToken, pipeline);

    /// <inheritdoc/>
    public Task<SKContext> RunAsync(string input, CancellationToken cancellationToken, params ISKFunction[] pipeline)
        => this.RunAsync(new ContextVariables(input), cancellationToken, pipeline);

    /// <inheritdoc/>
    public async Task<SKContext> RunAsync(ContextVariables variables, CancellationToken cancellationToken, params ISKFunction[] pipeline)
    {
        var context = new SKContext(
            variables,
            this._memory,
            this._skillCollection.ReadOnlySkillCollection,
            this._log,
            cancellationToken);

        int pipelineStepCount = -1;
        foreach (ISKFunction f in pipeline)
        {
            if (context.ErrorOccurred)
            {
                this._log.LogError(
                    context.LastException,
                    "Something went wrong in pipeline step {0}:'{1}'", pipelineStepCount, context.LastErrorDescription);
                return context;
            }

            pipelineStepCount++;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                context = await f.InvokeAsync(context);

                if (context.ErrorOccurred)
                {
                    this._log.LogError("Function call fail during pipeline step {0}: {1}.{2}", pipelineStepCount, f.SkillName, f.Name);
                    return context;
                }
            }
#pragma warning disable CA1031 // We need to catch all exceptions to handle the execution state
            catch (Exception e) when (!e.IsCriticalException())
            {
                this._log.LogError(e, "Something went wrong in pipeline step {0}: {1}.{2}. Error: {3}", pipelineStepCount, f.SkillName, f.Name, e.Message);
                context.Fail(e.Message, e);
                return context;
            }
#pragma warning restore CA1031
        }

        return context;
    }

    /// <inheritdoc/>
    public ISKFunction Func(string skillName, string functionName)
    {
        if (this.Skills.HasNativeFunction(skillName, functionName))
        {
            return this.Skills.GetNativeFunction(skillName, functionName);
        }

        return this.Skills.GetSemanticFunction(skillName, functionName);
    }

    /// <inheritdoc/>
    public SKContext CreateNewContext()
    {
        return new SKContext(
            new ContextVariables(),
            this._memory,
            this._skillCollection.ReadOnlySkillCollection,
            this._log);
    }

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    public void Dispose()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (this._memory is IDisposable mem) { mem.Dispose(); }

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (this._skillCollection is IDisposable reg) { reg.Dispose(); }
    }

    #region private ================================================================================

    private readonly ILogger _log;
    private readonly KernelConfig _config;
    private readonly ISkillCollection _skillCollection;
    private ISemanticTextMemory _memory;
    private readonly IPromptTemplateEngine _promptTemplateEngine;

    private ISKFunction CreateSemanticFunction(
        string skillName,
        string functionName,
        SemanticFunctionConfig functionConfig)
    {
        if (!functionConfig.PromptTemplateConfig.Type.EqualsIgnoreCase("completion"))
        {
            throw new AIException(
                AIException.ErrorCodes.FunctionTypeNotSupported,
                $"Function type not supported: {functionConfig.PromptTemplateConfig}");
        }

        ISKFunction func = SKFunction.FromSemanticConfig(skillName, functionName, functionConfig);

        // Connect the function to the current kernel skill collection, in case the function
        // is invoked manually without a context and without a way to find other functions.
        func.SetDefaultSkillCollection(this.Skills);

        func.SetAIConfiguration(CompleteRequestSettings.FromCompletionConfig(functionConfig.PromptTemplateConfig.Completion));

        // TODO: allow to postpone this (e.g. use lazy init), allow to create semantic functions without a default backend
        var backend = this._config.GetCompletionBackend(functionConfig.PromptTemplateConfig.DefaultBackends.FirstOrDefault());

        switch (backend)
        {
            case AzureOpenAIConfig azureBackendConfig:
                func.SetAIBackend(() => new AzureTextCompletion(
                    azureBackendConfig.DeploymentName,
                    azureBackendConfig.Endpoint,
                    azureBackendConfig.APIKey,
                    azureBackendConfig.APIVersion,
                    this._log,
                    this._config.HttpHandlerFactory));
                break;

            case OpenAIConfig openAiConfig:
                func.SetAIBackend(() => new OpenAITextCompletion(
                    openAiConfig.ModelId,
                    openAiConfig.APIKey,
                    openAiConfig.OrgId,
                    this._log,
                    this._config.HttpHandlerFactory));
                break;

            default:
                throw new AIException(
                    AIException.ErrorCodes.InvalidConfiguration,
                    $"Unknown/unsupported backend configuration type {backend.GetType():G}, unable to prepare semantic function. " +
                    $"Function description: {functionConfig.PromptTemplateConfig.Description}");
        }

        return func;
    }

    /// <summary>
    /// Import a skill into the kernel skill collection, so that semantic functions and pipelines can consume its functions.
    /// </summary>
    /// <param name="skillInstance">Skill class instance</param>
    /// <param name="skillName">Skill name, used to group functions under a shared namespace</param>
    /// <param name="log">Application logger</param>
    /// <returns>List of functions imported from the given class instance</returns>
    private static IList<ISKFunction> ImportSkill(object skillInstance, string skillName, ILogger log)
    {
        log.LogTrace("Importing skill name: {0}", skillName);
        MethodInfo[] methods = skillInstance.GetType()
            .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod);
        log.LogTrace("Methods found {0}", methods.Length);

        // Filter out null functions
        IEnumerable<ISKFunction> functions = from method in methods select SKFunction.FromNativeMethod(method, skillInstance, skillName, log);
        List<ISKFunction> result = (from function in functions where function != null select function).ToList();
        log.LogTrace("Methods imported {0}", result.Count);

        // Fail if two functions have the same name
        var uniquenessCheck = new HashSet<string>(from x in result select x.Name, StringComparer.OrdinalIgnoreCase);
        if (result.Count > uniquenessCheck.Count)
        {
            throw new KernelException(
                KernelException.ErrorCodes.FunctionOverloadNotSupported,
                "Function overloads are not supported, please differentiate function names");
        }

        return result;
    }

    #endregion
}
