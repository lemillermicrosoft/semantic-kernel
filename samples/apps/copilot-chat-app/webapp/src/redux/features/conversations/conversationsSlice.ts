// Copyright (c) Microsoft. All rights reserved.

import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { IChatMessage } from '../../../libs/models/ChatMessage';
import { ChatState } from './ChatState';
import { Conversations, ConversationsState, ConversationTitleChange, initialState } from './ConversationsState';

export const conversationsSlice = createSlice({
    name: 'conversations',
    initialState,
    reducers: {
        incrementBotProfilePictureIndex: (state: ConversationsState) => {
            state.botProfilePictureIndex = ++state.botProfilePictureIndex % 5;
        },
        setConversations: (state: ConversationsState, action: PayloadAction<Conversations>) => {
            state.conversations = action.payload;
        },
        editConversationTitle: (state: ConversationsState, action: PayloadAction<ConversationTitleChange>) => {
            const id = action.payload.id;
            const newTitle = action.payload.newTitle;
            state.conversations[id].title = newTitle;
            frontLoadChat(state, id);
        },
        setSelectedConversation: (state: ConversationsState, action: PayloadAction<string>) => {
            state.selectedId = action.payload;
        },
        addConversation: (state: ConversationsState, action: PayloadAction<ChatState>) => {
            const newId = action.payload.id ?? '';
            state.conversations = { [newId]: action.payload, ...state.conversations };
        },
        updateConversation: (
            state: ConversationsState,
            action: PayloadAction<{ message: IChatMessage; chatId?: string; nextAction?: string }>,
        ) => {
            const { message, chatId, nextAction } = action.payload;
            const id = chatId ?? state.selectedId;
            state.conversations[id].messages.push(message);
            state.conversations[id].nextAction = nextAction ?? '';
            frontLoadChat(state, id);
        },
        setChatSessionModeratingMessage: (
            state: ConversationsState,
            action: PayloadAction<{ message: IChatMessage; chatId?: string }>,
        ) => {
            const { message, chatId } = action.payload;
            const id = chatId ?? state.selectedId;
            state.conversations[id].moderatingMessages.push(message.userId + message.timestamp);
            frontLoadChat(state, id);
        },
        removeChatSessionModeratingMessage: (
            state: ConversationsState,
            action: PayloadAction<{ message: IChatMessage; chatId?: string }>,
        ) => {
            const { message, chatId } = action.payload;
            const id = chatId ?? state.selectedId;

            const index = state.conversations[id].moderatingMessages.indexOf(message.userId + message.timestamp);

            if (index !== -1) {
                state.conversations[id].moderatingMessages.splice(index, 1);
                frontLoadChat(state, id);
            }
        },
    },
});

export const {
    incrementBotProfilePictureIndex,
    setConversations,
    editConversationTitle,
    setSelectedConversation,
    addConversation,
    updateConversation,
    setChatSessionModeratingMessage,
    removeChatSessionModeratingMessage,
} = conversationsSlice.actions;

export default conversationsSlice.reducer;

const frontLoadChat = (state: ConversationsState, id: string) => {
    const conversation = state.conversations[id];
    delete state.conversations[id];
    state.conversations = { [id]: conversation, ...state.conversations };
};
