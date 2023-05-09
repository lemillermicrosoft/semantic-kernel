// Copyright (c) Microsoft. All rights reserved.

export interface IAskResult {
    value: string;
    variables: Variables;
    nextAction: string; // TODO -- or just get from Variables?
}

export type Variables = { [key: string]: string }[];
