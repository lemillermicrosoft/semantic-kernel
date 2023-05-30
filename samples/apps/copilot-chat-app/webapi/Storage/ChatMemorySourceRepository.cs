﻿// Copyright (c) Microsoft. All rights reserved.

using SemanticKernel.Service.Model;

namespace SemanticKernel.Service.Storage;

public class ChatMemorySourceRepository : Repository<MemorySource>
{
    /// <summary>
    /// Initializes a new instance of the ChatMemorySourceRepository class.
    /// </summary>
    /// <param name="storageContext">The storage context.</param>
    public ChatMemorySourceRepository(IStorageContext<MemorySource> storageContext)
        : base(storageContext)
    {
    }

    /// <summary>
    /// Finds chat memory sources by chat session id
    /// </summary>
    /// <param name="chatSessionId">The chat session id.</param>
    /// <returns>A list of memory sources of the given chat session.</returns>
    public Task<IEnumerable<MemorySource>> FindByChatSessionIdAsync(string chatSessionId)
    {
        return base.StorageContext.QueryEntitiesAsync(e => e.ChatSessionId == chatSessionId);
    }

    /// <summary>
    /// Finds chat memory sources by name
    /// </summary>
    /// <param name="name">Name</param>
    /// <returns>A list of memory sources with the given name.</returns>
    public Task<IEnumerable<MemorySource>> FindByNameAsync(string name)
    {
        return base.StorageContext.QueryEntitiesAsync(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
