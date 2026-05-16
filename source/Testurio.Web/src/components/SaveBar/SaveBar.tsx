import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import { useTheme, type Theme } from '@mui/material/styles';

export type SaveBarState = 'clean' | 'dirty' | 'saving' | 'saved';

export interface SaveBarProps {
  state: SaveBarState;
  onClick: () => void;
}

export default function SaveBar({ state, onClick }: SaveBarProps) {
  const { t } = useTranslation('project');
  const theme = useTheme();
  const styles = getStyles(theme);

  const isDisabled = state === 'clean' || state === 'saving' || state === 'saved';
  const isSaving = state === 'saving';
  const isSaved = state === 'saved';

  return (
    <Box sx={styles.root}>
      <Button
        variant="contained"
        color={isSaved ? 'success' : 'primary'}
        disabled={isDisabled}
        onClick={onClick}
        startIcon={isSaving ? <CircularProgress size={16} color="inherit" /> : null}
      >
        {state === 'clean' && t('saveBar.noChanges')}
        {state === 'dirty' && t('saveBar.save')}
        {state === 'saving' && t('saveBar.saving')}
        {state === 'saved' && t('saveBar.saved')}
      </Button>
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        justifyContent: 'flex-end',
        paddingTop: theme.spacing(2),
      },
    }),
    [theme],
  );
