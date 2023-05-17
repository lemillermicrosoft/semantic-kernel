// Copyright (c) Microsoft. All rights reserved.

import {
    TableBody,
    TableCell,
    TableRow,
    Table,
    TableHeader,
    TableHeaderCell,
    TableCellLayout,
    PresenceBadgeStatus,
    Avatar,
    shorthands,
    tokens,
    makeStyles,
} from '@fluentui/react-components';
import * as React from 'react';
import { DocumentPdfRegular } from '@fluentui/react-icons';

const useClasses = makeStyles({
    root: {
        ...shorthands.margin(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        backgroundColor: tokens.colorNeutralBackground1,
    },
    tableHeader: {
        fontWeight: '600',
    },
});

export const ChatResourceList: React.FC = () => {
    const classes = useClasses();

    const items = [
        {
            name: { label: 'Adventure Works Operating Manual.pdf', icon: <DocumentPdfRegular /> },
            updatedOn: { label: '7h ago', timestamp: 1 },
            sharedBy: { label: 'Blake Mustermann', status: 'available' },
        },
        /*{
            name: { label: 'Training recording', icon: <VideoRegular /> },
            updatedOn: { label: 'Yesterday at 1:45 PM', timestamp: 2 },
            sharedBy: { label: 'John Doe', status: 'away' },
        },
        {
            name: { label: 'Purchase order', icon: <DocumentRegular /> },
            updatedOn: { label: 'Tue at 9:30 AM', timestamp: 3 },
            sharedBy: { label: 'Jane Doe', status: 'offline' },
        },*/
    ];

    const columns = [
        { columnKey: 'name', label: 'Name' },
        { columnKey: 'updatedOn', label: 'Updated on' },
        { columnKey: 'sharedBy', label: 'Shared by' },
    ];

    return (
        <Table arial-label="External resource table" className={classes.root}>
            <TableHeader>
                <TableRow>
                    {columns.map((column) => (
                        <TableHeaderCell key={column.columnKey}>
                            <span className={classes.tableHeader}>{column.label}</span>
                        </TableHeaderCell>
                    ))}
                </TableRow>
            </TableHeader>
            <TableBody>
                {items.map((item) => (
                    <TableRow key={item.name.label}>
                        <TableCell>
                            <TableCellLayout media={item.name.icon}>
                                <a href=";">{item.name.label}</a>
                            </TableCellLayout>
                        </TableCell>
                        <TableCell>{item.updatedOn.label}</TableCell>
                        <TableCell>
                            <TableCellLayout
                                media={
                                    <Avatar
                                        aria-label={item.sharedBy.label}
                                        name={item.sharedBy.label}
                                        badge={{
                                            status: item.sharedBy.status as PresenceBadgeStatus,
                                        }}
                                    />
                                }
                            >
                                {item.sharedBy.label}
                            </TableCellLayout>
                        </TableCell>
                    </TableRow>
                ))}
            </TableBody>
        </Table>
    );
};
