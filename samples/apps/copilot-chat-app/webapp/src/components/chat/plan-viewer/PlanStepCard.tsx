import { Badge, Body1, Card, CardHeader, makeStyles, shorthands, Text, tokens } from '@fluentui/react-components';
import { IPlan, IPlanInput } from '../../../libs/models/Plan';
import { CopilotChatTokens } from '../../../styles';

const useClasses = makeStyles({
    card: {
        ...shorthands.margin('auto'),
        width: '700px',
        maxWidth: '100%',
    },
    header: {
        color: CopilotChatTokens.titleColor,
    },
    inputs: {
        display: 'flex',
        ...shorthands.gap('8px'),
    },
    bar: {
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        width: '4px',
        backgroundColor: CopilotChatTokens.titleColor,
    },
    flexRow: {
        display: 'flex',
        flexDirection: 'row',
    },
    flexColumn: {
        display: 'flex',
        flexDirection: 'column',
        marginLeft: '8px',
        marginTop: '4px',
        marginBottom: '4px',
        ...shorthands.gap('8px'),
    },
    singleLine: {
        ...shorthands.overflow('hidden'),
        lineHeight: '16px',
        display: '-webkit-box',
        WebkitLineClamp: 1,
        WebkitBoxOrient: 'vertical',
        width: '650px',
        fontSize: '12px',
    },
});

interface PlanStepCardProps {
    index: number;
    step: IPlan;
}

export const PlanStepCard: React.FC<PlanStepCardProps> = ({ index, step }) => {
    const classes = useClasses();
    var stepCount = 1;

    const numbersAsStrings = ['Zero', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine'];
    const stepNumber = numbersAsStrings[index];

    return (
        <Card className={classes.card}>
            <div className={classes.flexRow}>
                <div className={classes.bar} />
                <div className={classes.flexColumn}>
                    <CardHeader
                        header={
                            <Body1>
                                <b className={classes.header}>
                                    Step {stepNumber}
                                    {(step.skill || step.function) && ' •'}
                                </b>
                                {step.skill || step.function ? ` ${step.skill}.${step.function}` : ''}
                                <br />
                            </Body1>
                        }
                    />
                    {step.description && (
                        <div className={classes.singleLine}>
                            <Text weight="semibold">About: </Text> <Text>{step.description}</Text>
                        </div>
                    )}
                    <div className={classes.inputs}>
                        {step.stepInputs.length > 0 && <Text weight="semibold">Inputs: </Text>}
                        {step.stepInputs.map((input: IPlanInput) => {
                            return (
                                <Badge color="informative" shape="rounded" appearance="tint">
                                    {`${input.Key}: ${input.Value}`}
                                </Badge>
                            );
                        })}
                    </div>
                    {step.steps && step.steps.map((s: IPlan) => <PlanStepCard index={stepCount++} step={s} />)}
                </div>
            </div>
        </Card>
    );
};
