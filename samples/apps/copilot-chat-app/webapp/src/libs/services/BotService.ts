// Copyright (c) Microsoft. All rights reserved.

import { Bot } from '../models/Bot';
import { BaseService } from './BaseService';

export class BotService extends BaseService {
    public downloadAsync = async (
        chatId: string,
        userId: string,
        accessToken: string,
        planOnly: boolean,
    ): Promise<any> => {
        const prefix = planOnly ? 'lesson' : '';
        const result = await this.getResponseAsync<any>(
            {
                commandPath: `${prefix}bot/download/${chatId}/${userId}`,
                method: 'GET',
            },
            accessToken,
        );

        return result;
    };

    public uploadAsync = async (bot: Bot, userId: string, accessToken: string): Promise<any> => {
        // TODO: return type
        const result = await this.getResponseAsync<any>(
            {
                commandPath: `bot/upload?userId=${userId}`,
                method: 'Post',
                body: bot,
            },
            accessToken,
        );

        return result;
    };
}
