'use client';

import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { useTheme, type Theme } from '@mui/material/styles';
import type { PMToolType, WebhookSetupInfo } from '@/types/pmTool.types';

export interface WebhookSetupPanelProps {
  pmTool: PMToolType;
  setup: WebhookSetupInfo;
  isRegenerating: boolean;
  onRegenerate: () => void;
}

export default function WebhookSetupPanel({
  pmTool,
  setup,
  isRegenerating,
  onRegenerate,
}: WebhookSetupPanelProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [confirmOpen, setConfirmOpen] = useState(false);
  const [urlCopied, setUrlCopied] = useState(false);
  const [secretCopied, setSecretCopied] = useState(false);

  const handleCopyUrl = useCallback(async () => {
    await navigator.clipboard.writeText(setup.webhookUrl);
    setUrlCopied(true);
    setTimeout(() => setUrlCopied(false), 2000);
  }, [setup.webhookUrl]);

  const handleCopySecret = useCallback(async () => {
    if (!setup.isMasked) {
      await navigator.clipboard.writeText(setup.webhookSecret);
      setSecretCopied(true);
      setTimeout(() => setSecretCopied(false), 2000);
    }
  }, [setup.isMasked, setup.webhookSecret]);

  const handleRegenerateConfirm = useCallback(() => {
    setConfirmOpen(false);
    onRegenerate();
  }, [onRegenerate]);

  const handleRegenerateCancel = useCallback(() => {
    setConfirmOpen(false);
  }, []);

  const instructionKey =
    pmTool === 'ado' ? 'webhookSetup.instructionsAdo' : 'webhookSetup.instructionsJira';

  return (
    <Box sx={styles.root}>
      <Typography variant="body2" sx={styles.sectionLabel}>
        {t('webhookSetup.title')}
      </Typography>

      <Typography variant="body2" color="text.secondary">
        {t(instructionKey)}
      </Typography>

      <TextField
        label={t('webhookSetup.urlLabel')}
        value={setup.webhookUrl}
        InputProps={{
          readOnly: true,
          endAdornment: (
            <InputAdornment position="end">
              <IconButton onClick={handleCopyUrl} size="small" aria-label={t('webhookSetup.copyUrl')}>
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </InputAdornment>
          ),
        }}
        fullWidth
      />
      {urlCopied && (
        <Typography variant="caption" color="success.main">
          {t('webhookSetup.copied')}
        </Typography>
      )}

      <TextField
        label={t('webhookSetup.secretLabel')}
        value={setup.webhookSecret}
        type={setup.isMasked ? 'password' : 'text'}
        InputProps={{
          readOnly: true,
          endAdornment: !setup.isMasked ? (
            <InputAdornment position="end">
              <IconButton
                onClick={handleCopySecret}
                size="small"
                aria-label={t('webhookSetup.copySecret')}
              >
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </InputAdornment>
          ) : undefined,
        }}
        fullWidth
      />
      {secretCopied && (
        <Typography variant="caption" color="success.main">
          {t('webhookSetup.copied')}
        </Typography>
      )}

      {setup.isMasked && (
        <Alert severity="info" sx={styles.maskedAlert}>
          {t('webhookSetup.secretMaskedInfo')}
        </Alert>
      )}

      <Button
        variant="outlined"
        color="warning"
        onClick={() => setConfirmOpen(true)}
        disabled={isRegenerating}
        startIcon={isRegenerating ? <CircularProgress size={16} color="inherit" /> : null}
        sx={styles.regenerateButton}
      >
        {t('webhookSetup.regenerate')}
      </Button>

      <Dialog open={confirmOpen} onClose={handleRegenerateCancel}>
        <DialogTitle>{t('webhookSetup.regenerateDialog.title')}</DialogTitle>
        <DialogContent>
          <DialogContentText>{t('webhookSetup.regenerateDialog.message')}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleRegenerateCancel}>{t('common.cancel')}</Button>
          <Button onClick={handleRegenerateConfirm} color="warning" variant="contained">
            {t('webhookSetup.regenerateDialog.confirm')}
          </Button>
        </DialogActions>
      </Dialog>
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
      sectionLabel: {
        fontWeight: 600,
        textTransform: 'uppercase' as const,
        letterSpacing: '0.07em',
        color: theme.palette.text.secondary,
        fontSize: theme.typography.caption.fontSize,
        marginBottom: theme.spacing(0.5),
      },
      maskedAlert: {
        width: '100%',
      },
      regenerateButton: {
        alignSelf: 'flex-start',
      },
    }),
    [theme],
  );
