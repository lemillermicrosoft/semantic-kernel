import { IPlan, IPlanInput } from '../models/Plan';

export const isPlan = (object: string) => {
    // backslash has to be escaped since it's a JSON string
    // eslint-disable-next-line
    const planPrefix = `Microsoft.SemanticKernel.Planning.Plan`; // TODO make this better, teresa's pattern is likely better.
    return object.indexOf(planPrefix) !== -1;
};

export const parsePlan = (response: string): IPlan | null => {
    if (isPlan(response)) {
        const parsedResponse = JSON.parse(response);
        const plan = parsedResponse;

        const levelDepth = 1; // min is 0

        return {
            description: plan.description.trim(),
            stepInputs: extractPlanInputs(plan),
            skill: plan.skill_name.replace('Microsoft.SemanticKernel.Planning.Plan', ''),
            function: plan.name.replace('Microsoft.SemanticKernel.Planning.Plan', ''),
            steps: extractPlanSteps(plan, levelDepth),
        };
    }
    return null;
};

const extractPlanInputs = (plan: any) => {
    const planInputs: IPlanInput[] = [];
    for (var input in plan.state) {
        if (
            // Omit reserved context variable names
            plan.state[input].Key !== 'INPUT' &&
            plan.state[input].Key !== 'server_url' &&
            plan.state[input].Key !== 'action' && // todo clean this up on backend before saving?
            plan.state[input].Key !== 'stepLabel' &&
            plan.state[input].Key !== 'goalLabel' &&
            plan.state[input].Key !== 'content' &&
            plan.state[input].Key !== 'Parameters'
        ) {
            planInputs.push(plan.state[input]);
        }
    }

    for (var input in plan.parameters) {
        if (
            // Omit reserved context variable names
            plan.parameters[input].Key !== 'action' &&
            plan.parameters[input].Key !== 'stepLabel' &&
            plan.parameters[input].Key !== 'INPUT'
        ) {
            planInputs.push(plan.parameters[input]);
        }
    }

    return planInputs;
};

const extractPlanSteps = (plan: any, levelDepth: number) => {
    const planSteps = plan.steps;
    console.log(`Extracting n=${planSteps.length} steps from plan`);
    return planSteps.map((step: any) => {
        return {
            skill: step['skill_name'].replace('Microsoft.SemanticKernel.Planning.Plan', ''),
            function: step['name'].replace('Microsoft.SemanticKernel.Planning.Plan', ''),
            description: step['description'],
            stepInputs: extractPlanInputs(step),
            steps: levelDepth > 0 ? extractPlanSteps(step, levelDepth - 1) : [],
        };
    });
};
