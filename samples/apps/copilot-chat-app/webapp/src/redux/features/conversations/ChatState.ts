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
    botBadge?: ChatBadge;
    // HACK. Since the client insert user input without waiting for the id from the backend. We hack the solution to create a temporary id = userid + timestamp. If the message id is presented, it means it's under content moderation analysis.
    moderatingMessages: string[];
}

export enum ChatBadge {
    Warning = 1, // requires human attention.
    External, // the source of the bot is external.
}
