// Copyright (c) Microsoft. All rights reserved.

export interface IAsk {
    input: string;
    nextAction: string;
    variables?: IAskVariables[];
}

export interface IAskVariables {
    key: string;
    value: string;
}
