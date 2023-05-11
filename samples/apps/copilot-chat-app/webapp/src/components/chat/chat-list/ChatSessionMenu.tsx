import { Button, Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, Tooltip } from '@fluentui/react-components';
import { MoreHorizontal24Regular } from '@fluentui/react-icons';
import React /*, { useCallback }*/ from 'react';
//import { useChat } from '../../../libs/useChat';

interface ChatSessionMenuProps {
    chatId: string;
    chatTitle: string;
}

export const ChatSessionMenu: React.FC<ChatSessionMenuProps> = (/*{ chatId, chatTitle }*/) => {
    // const chat = useChat();

    /*const onForkConversation = useCallback(() => {
        chat.cloneChat(chatId, chatTitle);
    }, [chat, chatId, chatTitle]);*/

    return (
        <Menu>
            <MenuTrigger disableButtonEnhancement>
                <Tooltip content="More options" relationship="label" positioning="after" showDelay={250}>
                    <Button
                        aria-haspopup="true"
                        aria-hidden={true}
                        icon={<MoreHorizontal24Regular />}
                        appearance="transparent"
                    />
                </Tooltip>
            </MenuTrigger>
            <MenuPopover>
                <MenuList>
                    <MenuItem onClick={() => {} /*onForkConversation*/}>Fork</MenuItem>
                    <MenuItem disabled>Delete</MenuItem>
                </MenuList>
            </MenuPopover>
        </Menu>
    );
};
