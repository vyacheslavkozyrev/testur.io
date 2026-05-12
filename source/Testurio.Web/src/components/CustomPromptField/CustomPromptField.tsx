'use client';

import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { usePromptCheck } from '@/hooks/useProject';
import type { PromptCheckFeedback } from '@/types/project.types';

const MAX_PROMPT_LENGTH = 5000;

/** Keywords that may indicate a conflict between the custom prompt and common testing-strategy terms. */
const CONFLICT_KEYWORDS = ['only', 'never', 'do not', 'skip', 'ignore', 'exclude', 'no '];

function detectConflict(customPrompt: string, testingStrategy: string): boolean {
  if (!customPrompt.trim() || !testingStrategy.trim()) return false;
  const lower = customPrompt.toLowerCase();
  return CONFLICT_KEYWORDS.some((kw) => lower.includes(kw));
}

export interface CustomPromptFieldProps {
  projectId: string;
  testingStrategy: string;
  value: string;
  onChange: (value: string) => void;
}

export default function CustomPromptField({
  projectId,
  testingStrategy,
  value,
  onChange,
}: CustomPromptFieldProps) {
  const { t } = useTranslation('project');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [feedback, setFeedback] = useState<PromptCheckFeedback | null>(null);
  const promptCheck = usePromptCheck(projectId);

  const charCount = value.length;
  const isOverLimit = charCount > MAX_PROMPT_LENGTH;
  const hasConflict = useMemo(
    () => detectConflict(value, testingStrategy),
    [value, testingStrategy],
  );

  const previewText = useMemo(() => {
    const base = t('customPrompt.preview.basePrompt');
    const strategy = testingStrategy.trim()
      ? `\n\n${t('customPrompt.preview.strategyLabel')}\n${testingStrategy.trim()}`
      : '';
    const custom = value.trim()
      ? `\n\n${t('customPrompt.preview.customLabel')}\n${value.trim()}`
      : '';
    return `${base}${strategy}${custom}`;
  }, [t, testingStrategy, value]);

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      onChange(e.target.value);
      // AC-027: clear stale feedback whenever the user edits the prompt field.
      if (feedback) {
        setFeedback(null);
      }
    },
    [onChange, feedback],
  );

  const handleCheckPrompt = useCallback(() => {
    if (!value.trim()) return;
    promptCheck.mutate(
      { customPrompt: value },
      {
        onSuccess: (result) => {
          setFeedback(result);
        },
      },
    );
  }, [promptCheck, value]);

  return (
    <Box sx={styles.root}>
      <TextField
        label={t('customPrompt.field.label')}
        placeholder={t('customPrompt.field.placeholder')}
        multiline
        minRows={4}
        value={value}
        onChange={handleChange}
        error={isOverLimit}
        helperText={
          isOverLimit
            ? t('customPrompt.field.overLimit')
            : `${charCount} / ${MAX_PROMPT_LENGTH}`
        }
        fullWidth
        inputProps={{ maxLength: MAX_PROMPT_LENGTH }}
      />

      {hasConflict && (
        <Alert severity="warning" sx={styles.warning}>
          {t('customPrompt.field.conflictWarning')}
        </Alert>
      )}

      <Box sx={styles.checkRow}>
        <Button
          variant="outlined"
          size="small"
          disabled={!value.trim() || promptCheck.isPending}
          onClick={handleCheckPrompt}
          startIcon={promptCheck.isPending ? <CircularProgress size={14} color="inherit" /> : null}
        >
          {t('customPrompt.check.button')}
        </Button>
      </Box>

      {promptCheck.isError && (
        <Alert severity="error" sx={styles.feedbackAlert}>
          {t('customPrompt.check.error')}
        </Alert>
      )}

      {feedback && (
        <Paper variant="outlined" sx={styles.feedbackPanel}>
          <Typography variant="subtitle2" sx={styles.feedbackTitle}>
            {t('customPrompt.check.resultsTitle')}
          </Typography>
          <Divider sx={styles.divider} />
          <FeedbackDimension
            label={t('customPrompt.check.clarity')}
            assessment={feedback.clarity.assessment}
            suggestion={feedback.clarity.suggestion}
          />
          <FeedbackDimension
            label={t('customPrompt.check.specificity')}
            assessment={feedback.specificity.assessment}
            suggestion={feedback.specificity.suggestion}
          />
          <FeedbackDimension
            label={t('customPrompt.check.potentialConflicts')}
            assessment={feedback.potentialConflicts.assessment}
            suggestion={feedback.potentialConflicts.suggestion}
          />
        </Paper>
      )}

      <Paper variant="outlined" sx={styles.previewPanel}>
        <Typography variant="subtitle2" sx={styles.previewTitle}>
          {t('customPrompt.preview.title')}
        </Typography>
        <Typography variant="caption" sx={styles.previewReadOnly}>
          {t('customPrompt.preview.readOnly')}
        </Typography>
        <Box component="pre" sx={styles.previewContent}>
          {previewText}
          {!value.trim() && (
            <Typography component="span" sx={styles.previewNoCustom}>
              {'\n\n'}{t('customPrompt.preview.noCustomPrompt')}
            </Typography>
          )}
        </Box>
      </Paper>
    </Box>
  );
}

interface FeedbackDimensionProps {
  label: string;
  assessment: string;
  suggestion: string | null;
}

function FeedbackDimension({ label, assessment, suggestion }: FeedbackDimensionProps) {
  const theme = useTheme();
  const styles = getFeedbackDimensionStyles(theme);
  return (
    <Box sx={styles.root}>
      <Typography variant="body2" sx={styles.label}>
        {label}
      </Typography>
      <Typography variant="body2">{assessment}</Typography>
      {suggestion && (
        <Typography variant="body2" sx={styles.suggestion}>
          {suggestion}
        </Typography>
      )}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      warning: {
        width: '100%',
      },
      checkRow: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(1),
      },
      feedbackAlert: {
        width: '100%',
      },
      feedbackPanel: {
        padding: theme.spacing(2),
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1.5),
      },
      feedbackTitle: {
        ...theme.typography.subtitle2,
        fontWeight: 600,
      },
      divider: {
        marginBottom: theme.spacing(0.5),
      },
      previewPanel: {
        padding: theme.spacing(2),
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1),
        backgroundColor: theme.palette.action.hover,
      },
      previewTitle: {
        ...theme.typography.subtitle2,
        fontWeight: 600,
      },
      previewReadOnly: {
        color: theme.palette.text.secondary,
        fontStyle: 'italic',
      },
      previewContent: {
        fontFamily: 'monospace',
        fontSize: theme.typography.caption.fontSize,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        margin: 0,
        color: theme.palette.text.primary,
      },
      previewNoCustom: {
        color: theme.palette.text.disabled,
        fontStyle: 'italic',
      },
    }),
    [theme],
  );

const getFeedbackDimensionStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(0.5),
      },
      label: {
        fontWeight: 600,
        color: theme.palette.text.secondary,
      },
      suggestion: {
        color: theme.palette.info.main,
        fontStyle: 'italic',
      },
    }),
    [theme],
  );
