import React from 'react';
import { makeStyles } from '@fluentui/react-components';
import { AdaptiveCard as ReactAdaptiveCard } from 'adaptivecards-react';

const hostConfig = {
    fontFamily: 'Segoe UI, Helvetica Neue, sans-serif',
};

const useClasses = makeStyles({
    root: {
        backgroundColor: '#fff',
        marginTop: '10px',
        marginBottom: '10px',
    },
});

interface AdaptiveCardProps {
    payload: any;
}

export const AdaptiveCard: React.FC<AdaptiveCardProps> = ({ payload }) => {
    const classes = useClasses();

    return (
        <div className={classes.root}>
            <ReactAdaptiveCard payload={payload} hostConfig={hostConfig} />
        </div>
    );
};
