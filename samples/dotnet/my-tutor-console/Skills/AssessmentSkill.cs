// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Skills;

// AssessmentSkill
// Identify current knowledge or skills level to determine where the individual needs to improve or learn more
// Conduct self-assessment, observation, or formal testing
public class AssessmentSkill
{
    // GetAssessment
    [SKFunction(description: "Get assessment")]
    public SKContext GetAssessment(SKContext context)
    {
        return context;
    }

    // SetAssessment
    [SKFunction(description: "Set assessment")]
    public SKContext SetAssessment(SKContext context)
    {
        return context;
    }

    // GetAssessmentResults
    [SKFunction(description: "Get assessment results")]
    public SKContext GetAssessmentResults(SKContext context)
    {
        return context;
    }

    // SetAssessmentResults
    [SKFunction(description: "Set assessment results")]
    public SKContext SetAssessmentResults(SKContext context)
    {
        return context;
    }
}
