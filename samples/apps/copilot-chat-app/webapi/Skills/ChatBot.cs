// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace SemanticKernel.Service.Skills;

/// <summary>
/// A lightweight wrapper around a kernel to allow for curating which skills are available to it.
/// </summary>
public class ChatBot
{
    /// <summary>
    /// The bots kernel.
    /// </summary>
    public IKernel Kernel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatBot"/> class.
    /// </summary>
    /// <param name="plannerKernel">The chatbots kernel.</param>
    public ChatBot(IKernel plannerKernel)
    {
        this.Kernel = plannerKernel;
    }
}
