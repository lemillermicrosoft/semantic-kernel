import { Button, Text, makeStyles, shorthands } from '@fluentui/react-components';
import { useState } from 'react';
import { IPlan } from '../../../libs/models/Plan';
import { PlanStepCard } from './PlanStepCard';

const useClasses = makeStyles({
    container: {
        ...shorthands.gap('11px'),
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'baseline',
    },
    buttons: {
        display: 'flex',
        flexDirection: 'row',
        marginTop: '12px',
        marginBottom: '12px',
        ...shorthands.gap('16px'),
    },
});

interface PlanViewerProps {
    plan: IPlan;
    actionRequired?: boolean;
    learningPlan?: boolean;
    onSubmit: () => Promise<void>;
    onCancel: () => Promise<void>;
}

export const PlanViewer: React.FC<PlanViewerProps> = ({ plan, actionRequired, learningPlan, onSubmit, onCancel }) => {
    const classes = useClasses();
    var stepCount = 1;

    const [showButtons, setShowButtons] = useState(actionRequired && !learningPlan);
    const [showLearningButtons, setShowLearningButtons] = useState(learningPlan);

    const onCancelClick = () => {
        setShowButtons(false);
        setShowLearningButtons(false);
        onCancel();
    };

    const onProceedClick = () => {
        setShowButtons(false);
        setShowLearningButtons(false);
        onSubmit();
    };

    return (
        <div className={classes.container}>
            {actionRequired && !learningPlan && (
                <Text>Based on the request, Copilot Chat will run the following steps:</Text>
            )}
            {learningPlan && <Text>Here is a lesson plan we've created to guide a user through.</Text>}
            <Text weight="bold">{`Goal: ${plan.description}`}</Text>
            {plan.steps && plan.steps.map((step: IPlan) => <PlanStepCard index={stepCount++} step={step} />)}
            {showButtons && (
                <>
                    Would you like to proceed with the plan?
                    <div className={classes.buttons}>
                        <Button appearance="secondary" onClick={onCancelClick}>
                            No, cancel plan
                        </Button>
                        <Button type="submit" appearance="primary" onClick={onProceedClick}>
                            Yes, proceed
                        </Button>
                    </div>
                </>
            )}
            {!showButtons && showLearningButtons && (
                <>
                    Would you like to start the instruction?
                    <div className={classes.buttons}>
                        <Button appearance="secondary" onClick={onCancelClick}>
                            Save Plan
                        </Button>
                        <Button type="submit" appearance="primary" onClick={onProceedClick}>
                            Start Instruction
                        </Button>
                    </div>
                </>
            )}
        </div>
    );
};
