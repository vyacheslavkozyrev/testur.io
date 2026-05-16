'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';

interface WorkItemChipProps {
  type: string;
  onRemove: (type: string) => void;
}

function WorkItemChip({ type, onRemove }: WorkItemChipProps) {
  const handleDelete = useCallback(() => onRemove(type), [type, onRemove]);
  return <Chip label={type} onDelete={handleDelete} size="small" />;
}

export interface WorkItemTypeFilterProps {
  currentTypes: string[];
  onChange: (types: string[]) => void;
}

export default function WorkItemTypeFilter({
  currentTypes,
  onChange,
}: WorkItemTypeFilterProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [types, setTypes] = useState<string[]>(currentTypes);
  const [inputValue, setInputValue] = useState('');

  // Sync server data into local state only on the first non-empty resolution.
  // Subsequent changes come from user actions, not re-renders.
  const initializedRef = useRef(false);
  useEffect(() => {
    if (!initializedRef.current && currentTypes.length > 0) {
      setTypes(currentTypes);
      initializedRef.current = true;
    }
  }, [currentTypes]);

  const handleAddType = useCallback(() => {
    const trimmed = inputValue.trim();
    if (!trimmed || types.includes(trimmed)) {
      setInputValue('');
      return;
    }
    const next = [...types, trimmed];
    setTypes(next);
    setInputValue('');
    onChange(next);
  }, [inputValue, types, onChange]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        handleAddType();
      }
    },
    [handleAddType],
  );

  const handleRemoveType = useCallback((type: string) => {
    const next = types.filter((t) => t !== type);
    setTypes(next);
    onChange(next);
  }, [types, onChange]);

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setInputValue(e.target.value);
  }, []);

  return (
    <Box sx={styles.root}>
      <Typography variant="subtitle1" sx={styles.title}>
        {t('workItemTypeFilter.title')}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {t('workItemTypeFilter.description')}
      </Typography>

      <Box sx={styles.chips}>
        {types.map((type) => (
          <WorkItemChip key={type} type={type} onRemove={handleRemoveType} />
        ))}
      </Box>

      <Box sx={styles.inputRow}>
        <TextField
          size="small"
          label={t('workItemTypeFilter.inputLabel')}
          value={inputValue}
          onChange={handleInputChange}
          onKeyDown={handleKeyDown}
          sx={styles.input}
        />
        <Button variant="outlined" size="small" onClick={handleAddType} disabled={!inputValue.trim()}>
          {t('workItemTypeFilter.addButton')}
        </Button>
      </Box>
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      title: {
        ...theme.typography.subtitle1,
        color: theme.palette.text.primary,
      },
      chips: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: theme.spacing(1),
        minHeight: theme.spacing(4),
      },
      inputRow: {
        display: 'flex',
        gap: theme.spacing(1),
        alignItems: 'center',
      },
      input: {
        flex: 1,
      },
    }),
    [theme],
  );
