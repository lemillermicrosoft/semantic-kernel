import { Constants } from '../../../Constants';
import GithubIcon from '../../../assets/plugin-icons/github.png';
import JiraIcon from '../../../assets/plugin-icons/jira.png';
import GraphIcon from '../../../assets/plugin-icons/ms-graph.png';

/*
 * For each OpenAPI Spec you're supporting in the Kernel,
 * add all the relevant information here.
 */
export const enum Plugins {
    MsGraph = 'Microsoft Graph',
    Jira = 'Jira',
    GitHub = 'GitHub',
    HuggingFace = 'Hugging Face',
    VisionStudio = 'Azure Cognitive Service for Vision',
}

export const enum AuthHeaderTags {
    MsGraph = 'graph',
    Jira = 'jira',
    GitHub = 'github',
    HuggingFace = 'huggingface',
    VisionStudio = 'visionstudio',
}

export type PluginAuthRequirements = {
    username?: boolean;
    password?: boolean;
    personalAccessToken?: boolean;
    OAuth?: boolean;
    Msal?: boolean;
    scopes?: string[];
    helpLink?: string;
};

// Additional information required to enable OpenAPI skills, i.e., server-url
export type AdditionalApiProperties = {
    // Key should be the property name and in kebab case (valid format for request header),
    // make sure it matches exactly with the property name the API requires
    [key: string]: {
        required: boolean;
        helpLink?: string;
        value?: string;
        description?: string;
    };
};

export type Plugin = {
    name: Plugins;
    publisher: string;
    description: string;
    enabled: boolean;
    authRequirements: PluginAuthRequirements;
    headerTag: AuthHeaderTags;
    icon: string; // Can be imported as shown above or direct URL
    authData?: string; // token or encoded auth header value
    apiProperties?: AdditionalApiProperties;
};

export interface PluginsState {
    MsGraph: Plugin;
    Jira: Plugin;
    GitHub: Plugin;
    HuggingFace: Plugin;
    VisionStudio: Plugin;
}

export const initialState: PluginsState = {
    MsGraph: {
        name: Plugins.MsGraph,
        publisher: 'Microsoft',
        description: 'Use your Microsoft Account to access your personal Graph information and Microsoft services.',
        enabled: false,
        authRequirements: {
            Msal: true,
            scopes: Constants.msGraphScopes,
        },
        headerTag: AuthHeaderTags.MsGraph,
        icon: GraphIcon,
    },
    Jira: {
        name: Plugins.Jira,
        publisher: 'Atlassian',
        description: 'Authorize Copilot Chat to post and link with Jira when there are issues.',
        enabled: false,
        authRequirements: {
            username: true,
            personalAccessToken: true,
            helpLink: 'https://developer.atlassian.com/cloud/confluence/basic-auth-for-rest-apis/',
        },
        icon: JiraIcon,
        headerTag: AuthHeaderTags.Jira,
        apiProperties: {
            'server-url': {
                required: true,
                helpLink: 'https://confluence.atlassian.com/adminjiraserver/configuring-the-base-url-938847830.html',
            },
        },
    },
    GitHub: {
        name: Plugins.GitHub,
        publisher: 'Microsoft',
        description:
            'Integrate Github with Copilot Chat, i.e., allow Copilot ChatBot to list active Pull Requests for you.',
        enabled: false,
        authRequirements: {
            personalAccessToken: true,
            scopes: ['Read and Write access to pull requests'],
            helpLink:
                'https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token',
        },
        icon: GithubIcon,
        headerTag: AuthHeaderTags.GitHub,
        apiProperties: {
            owner: {
                required: false,
                description: 'account owner of repository. i.e., "microsoft"',
            },
            repo: {
                required: false,
                description: 'name of repository. i.e., "semantic-kernel"',
                helpLink: 'https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#list-pull-requests',
            },
        },
    },
    HuggingFace: {
        name: Plugins.HuggingFace,
        publisher: 'Hugging Face',
        description: 'Use your Hugging Face account to access your personal models and services.',
        enabled: false,
        authRequirements: {
            username: true,
            personalAccessToken: true,
            helpLink: 'https://huggingface.co/docs/hub/security-tokens',
        },
        icon: 'https://huggingface.co/front/assets/huggingface_logo.svg',
        headerTag: AuthHeaderTags.HuggingFace,
        apiProperties: {
            'server-url': {
                required: true,
                helpLink: '',
            },
        },
    },
    VisionStudio: {
        name: Plugins.VisionStudio,
        publisher: 'Microsoft',
        description: 'Use your custom vision model from Vision Studio.',
        enabled: false,
        authRequirements: {
            username: true,
            personalAccessToken: true,
            helpLink: '',
        },
        icon: 'https://portal.vision.cognitive.azure.com/dist/static/media/vision-header.f3f21b26.svg',
        headerTag: AuthHeaderTags.VisionStudio,
        apiProperties: {
            'server-url': {
                required: true,
                helpLink: '',
            },
        },
    },
};

export type EnablePluginPayload = {
    plugin: Plugins;
    username?: string;
    password?: string;
    accessToken?: string;
    apiProperties?: AdditionalApiProperties;
};
