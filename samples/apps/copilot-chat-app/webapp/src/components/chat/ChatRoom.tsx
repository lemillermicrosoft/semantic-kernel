// Copyright (c) Microsoft. All rights reserved.

import { useAccount, useMsal } from '@azure/msal-react';
import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import debug from 'debug';
import React from 'react';
import { Constants } from '../../Constants';
import { AuthorRoles } from '../../libs/models/ChatMessage';
import { useChat } from '../../libs/useChat';
import { useAppDispatch, useAppSelector } from '../../redux/app/hooks';
import { RootState } from '../../redux/app/store';
import {
    removeChatSessionModeratingMessage,
    setChatSessionModeratingMessage,
    updateConversation,
} from '../../redux/features/conversations/conversationsSlice';
import { ChatHistory } from './ChatHistory';
import { ChatInput } from './ChatInput';

const log = debug(Constants.debug.root).extend('chat-room');

const useClasses = makeStyles({
    root: {
        height: '94.5%',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        gridTemplateColumns: '1fr',
        gridTemplateRows: '1fr auto',
        gridTemplateAreas: "'history' 'input'",
    },
    history: {
        ...shorthands.gridArea('history'),
        ...shorthands.padding(tokens.spacingVerticalM),
        overflowY: 'auto',
        display: 'grid',
    },
    input: {
        ...shorthands.gridArea('input'),
        ...shorthands.padding(tokens.spacingVerticalM),
    },
});

export const ChatRoom: React.FC = () => {
    const { conversations, selectedId } = useAppSelector((state: RootState) => state.conversations);
    const { audience } = conversations[selectedId];
    const messages = conversations[selectedId].messages;
    const nextAction = conversations[selectedId].nextAction;
    const classes = useClasses();

    const { accounts } = useMsal();
    const account = useAccount(accounts[0] || {});

    const dispatch = useAppDispatch();
    const scrollViewTargetRef = React.useRef<HTMLDivElement>(null);
    const scrollTargetRef = React.useRef<HTMLDivElement>(null);
    const [shouldAutoScroll, setShouldAutoScroll] = React.useState(true);

    // hardcode to care only about the bot typing for now.
    const [isBotTyping, setIsBotTyping] = React.useState(false);

    const chat = useChat();

    React.useEffect(() => {
        if (!shouldAutoScroll) return;
        scrollToTarget(scrollTargetRef.current);
    }, [messages, audience, shouldAutoScroll]);

    React.useEffect(() => {
        const onScroll = () => {
            if (!scrollViewTargetRef.current) return;
            const { scrollTop, scrollHeight, clientHeight } = scrollViewTargetRef.current;
            const isAtBottom = scrollTop + clientHeight >= scrollHeight - 10;
            setShouldAutoScroll(isAtBottom);
        };

        if (!scrollViewTargetRef.current) return;

        const currentScrollViewTarget = scrollViewTargetRef.current;

        currentScrollViewTarget.addEventListener('scroll', onScroll);
        return () => {
            currentScrollViewTarget.removeEventListener('scroll', onScroll);
        };
    }, []);

    if (!account) {
        return null;
    }

    const handleSubmit = async (
        value: string,
        approvedPlanJson?: string,
        planUserIntent?: string,
        userCancelledPlan?: boolean,
        userSavedPlan?: boolean,
        userInvokePlanViaChat?: boolean,
    ) => {
        log('submitting user chat message');

        const isInputImage = value.startsWith('data:image');

        const chatInput = {
            timestamp: new Date().getTime(),
            userId: account?.homeAccountId,
            userName: account?.name as string,
            content: value,
            authorRole: AuthorRoles.User,
        };

        if (!!!userSavedPlan) {
            setIsBotTyping(true);

            // HACK
            if (isInputImage) {
                dispatch(setChatSessionModeratingMessage({ message: chatInput }));
            }

            dispatch(updateConversation({ message: chatInput }));
        }

        try {
            var response = await chat.getResponse(
                value,
                selectedId,
                nextAction ?? approvedPlanJson,
                approvedPlanJson,
                planUserIntent,
                userCancelledPlan,
                userSavedPlan,
                userInvokePlanViaChat,
            );

            // HACK
            if (isInputImage && response.content !== "It seems the content isn't appropriate.") {
                dispatch(removeChatSessionModeratingMessage({ message: chatInput }));
            }
        } finally {
            setIsBotTyping(false);
        }

        setShouldAutoScroll(true);
    };

    return (
        <div className={classes.root}>
            <div ref={scrollViewTargetRef} className={classes.history}>
                <ChatHistory audience={audience} messages={messages} onGetResponse={handleSubmit} />
                <div>
                    <div ref={scrollTargetRef} />
                </div>
            </div>
            <div className={classes.input}>
                <ChatInput isTyping={isBotTyping} onSubmit={handleSubmit} />
            </div>
        </div>
    );
};

const scrollToTarget = (element: HTMLElement | null) => {
    if (!element) return;
    element.scrollIntoView({ block: 'start', behavior: 'smooth' });
};
