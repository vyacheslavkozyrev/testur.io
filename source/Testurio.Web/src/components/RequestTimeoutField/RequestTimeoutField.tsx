'use client';

import { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import TextField from '@mui/material/TextField';
import { useTheme, type Theme } from '@mui/material/styles';

const MIN_TIMEOUT = 5;
const MAX_TIMEOUT = 120;

export interface RequestTimeoutFieldProps {
  value: number;
  onChange: (value: number) => void;
  error?: string | null;
}

export default function RequestTimeoutField({ value, onChange, error }: RequestTimeoutFieldProps) {
  const { t } = useTranslation('project');
  const theme = useTheme();
  const styles = getStyles(theme);

  const validationError = useMemo(() => {
    if (error) return error;
    if (value === null || value === undefined || Number.isNaN(value)) {
      return t('requestTimeout.validation.required');
    }
    if (!Number.isInteger(value) || value < MIN_TIMEOUT || value > MAX_TIMEOUT) {
      return t('requestTimeout.validation.range', { min: MIN_TIMEOUT, max: MAX_TIMEOUT });
    }
    return null;
  }, [value, error, t]);

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const parsed = parseInt(e.target.value, 10);
      onChange(Number.isNaN(parsed) ? 0 : parsed);
    },
    [onChange],
  );

  return (
    <TextField
      label={t('requestTimeout.field.label')}
      helperText={validationError ?? t('requestTimeout.field.helperText', { min: MIN_TIMEOUT, max: MAX_TIMEOUT })}
      error={Boolean(validationError)}
      value={value || ''}
      onChange={handleChange}
      type="number"
      inputProps={{ min: MIN_TIMEOUT, max: MAX_TIMEOUT, step: 1 }}
      required
      size="small"
      sx={styles.field}
    />
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      field: {
        maxWidth: 200,
        '& input::-webkit-inner-spin-button, & input::-webkit-outer-spin-button': {
          opacity: 1,
        },
      },
    }),
    [theme],
  );
