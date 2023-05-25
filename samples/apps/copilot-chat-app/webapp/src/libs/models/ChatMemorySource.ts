// Copyright (c) Microsoft. All rights reserved.

export interface ChatMemorySource {
    id: string;
    chatSessionId: string;
    sourceType: string;
    name: string;
    hyperlink?: string;
    sharedBy: string;
    updatedOn: number;
}
