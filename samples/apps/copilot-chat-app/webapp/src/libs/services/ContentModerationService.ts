import { BaseService } from './BaseService';

interface IContentModerationResult {
    [key: string]: { category: string; riskLevel: number };
}

export class ContentModerationService extends BaseService {
    public analyzeImageAsync = async (base64Image: string, accessToken: string): Promise<IContentModerationResult> => {
        const result = await this.getResponseAsync<IContentModerationResult>(
            {
                commandPath: 'contentModerator/image',
                method: 'POST',
                body: base64Image,
            },
            accessToken,
        );

        return result;
    };
}
