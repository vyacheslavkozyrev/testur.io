'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import FormHelperText from '@mui/material/FormHelperText';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';

export interface WorkItemTypeFilterProps {
  currentTypes: string[];
  isSaving: boolean;
  isError: boolean;
  onSave: (types: string[]) => void;
}

export default function WorkItemTypeFilter({
  currentTypes,
  isSaving,
  isError,
  onSave,
}: WorkItemTypeFilterProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [types, setTypes] = useState<string[]>(currentTypes);
  const [inputValue, setInputValue] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  // Sync server data into local state only on the first non-empty resolution (e.g. after
  // query loads). Subsequent changes must come from user actions, not re-renders.
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
    setTypes((prev) => [...prev, trimmed]);
    setInputValue('');
    setValidationError(null);
  }, [inputValue, types]);

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
    setTypes((prev) => prev.filter((t) => t !== type));
    setValidationError(null);
  }, []);

  const handleSave = useCallback(() => {
    if (types.length === 0) {
      setValidationError(t('workItemTypeFilter.validation.atLeastOne'));
      return;
    }
    setValidationError(null);
    onSave(types);
  }, [types, onSave, t]);

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
          <Chip
            key={type}
            label={type}
            onDelete={() => handleRemoveType(type)}
            size="small"
          />
        ))}
      </Box>

      <Box sx={styles.inputRow}>
        <TextField
          size="small"
          label={t('workItemTypeFilter.inputLabel')}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          sx={styles.input}
        />
        <Button variant="outlined" size="small" onClick={handleAddType} disabled={!inputValue.trim()}>
          {t('workItemTypeFilter.addButton')}
        </Button>
      </Box>

      {validationError && (
        <FormHelperText error>{validationError}</FormHelperText>
      )}

      {isError && (
        <Alert severity="error">{t('workItemTypeFilter.saveError')}</Alert>
      )}

      <Box>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={isSaving}
          startIcon={isSaving ? <CircularProgress size={16} /> : undefined}
        >
          {t('common.save')}
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
