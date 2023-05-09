// Copyright (c) Microsoft. All rights reserved.

import { IChatMessage } from '../../../libs/models/ChatMessage';
import { ChatUser } from '../../../libs/models/ChatUser';

export interface ChatState {
    id: string;
    title: string;
    audience: ChatUser[];
    messages: IChatMessage[];
    nextAction: string;
    botTypingTimestamp: number;
    botProfilePicture: string;
}
