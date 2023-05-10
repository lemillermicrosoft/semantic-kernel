import React from 'react';
import { AdaptiveCard } from './AdaptiveCard';

interface PeopleProfileCardProps {
    data: {
        title: string;
        displayName: string;
        description: string;
        profileImageUrl: string;
        profileUrl: string;
    };
}

export const PeopleProfileCard: React.FC<PeopleProfileCardProps> = ({ data }) => {
    const { title, displayName, description, profileImageUrl, profileUrl } = data;

    const payload = {
        type: 'AdaptiveCard',
        body: [
            {
                type: 'TextBlock',
                size: 'Medium',
                weight: 'Bolder',
                text: `${title}`,
                style: 'heading',
                wrap: true,
            },
            {
                type: 'ColumnSet',
                columns: [
                    {
                        type: 'Column',
                        items: [
                            {
                                type: 'Image',
                                style: 'Person',
                                url: `${profileImageUrl}`,
                                altText: `${displayName}`,
                                size: 'Small',
                            },
                        ],
                        width: 'auto',
                    },
                    {
                        type: 'Column',
                        items: [
                            {
                                type: 'TextBlock',
                                weight: 'Bolder',
                                text: `${displayName}`,
                                wrap: true,
                            },
                            {
                                type: 'TextBlock',
                                spacing: 'None',
                                text: `[LinkedIn](${profileUrl})`,
                                isSubtle: true,
                                wrap: true,
                            },
                        ],
                        width: 'stretch',
                    },
                ],
            },
            {
                type: 'TextBlock',
                text: `${description}`,
                wrap: true,
            },
        ],
        $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
        version: '1.5',
    };

    return <AdaptiveCard payload={payload} />;
};
