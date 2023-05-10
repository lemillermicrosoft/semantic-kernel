export type IPlanInput = {
    // These have to be capitalized to match the server response
    Key: string;
    Value: string;
};

export type IPlan = {
    skill: string;
    function: string;
    description: string;
    stepInputs: IPlanInput[];
    steps: IPlan[];
};
