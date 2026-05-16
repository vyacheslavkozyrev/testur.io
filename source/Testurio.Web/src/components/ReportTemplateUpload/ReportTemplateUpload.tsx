'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import {
  useUploadReportTemplate,
  useRemoveReportTemplate,
} from '@/hooks/useReportSettings';

const MAX_TEMPLATE_SIZE_BYTES = 100 * 1024; // 100 KB

export interface ReportTemplateUploadProps {
  projectId: string;
  currentFileName: string | null;
}

export default function ReportTemplateUpload({
  projectId,
  currentFileName,
}: ReportTemplateUploadProps) {
  const { t } = useTranslation('reportSettings');
  const theme = useTheme();
  const styles = getStyles(theme);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [tokenWarnings, setTokenWarnings] = useState<string[]>([]);
  const [confirmRemoveOpen, setConfirmRemoveOpen] = useState(false);

  const upload = useUploadReportTemplate(projectId);
  const remove = useRemoveReportTemplate(projectId);

  const { mutateAsync: uploadAsync } = upload;
  const { mutateAsync: removeAsync } = remove;

  const handleFileChange = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

      setValidationError(null);
      setTokenWarnings([]);

      // AC-002: only .md files
      if (!file.name.endsWith('.md')) {
        setValidationError(t('template.errorExtension'));
        if (fileInputRef.current) fileInputRef.current.value = '';
        return;
      }

      // AC-003: max 100 KB
      if (file.size > MAX_TEMPLATE_SIZE_BYTES) {
        setValidationError(t('template.errorSize'));
        if (fileInputRef.current) fileInputRef.current.value = '';
        return;
      }

      try {
        const result = await uploadAsync(file);
        if (result.warnings.length > 0) {
          setTokenWarnings(result.warnings);
        }
      } catch {
        // Error is accessible via upload.error
      }

      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    [uploadAsync, t],
  );

  const handleRemoveConfirm = useCallback(async () => {
    setConfirmRemoveOpen(false);
    try {
      await removeAsync();
    } catch {
      // Error is accessible via remove.isError
    }
  }, [removeAsync]);

  const hasTemplate = Boolean(currentFileName);

  return (
    <Box sx={styles.root}>
      <Typography variant="body2" sx={styles.sectionLabel}>
        {t('template.title')}
      </Typography>

      {hasTemplate ? (
        <Box sx={styles.existingTemplate}>
          <Typography variant="body2" sx={styles.fileName}>
            {currentFileName}
          </Typography>
          <Box sx={styles.actions}>
            <Button
              variant="outlined"
              size="small"
              onClick={() => fileInputRef.current?.click()}
              disabled={upload.isPending}
            >
              {t('template.replace')}
            </Button>
            <Button
              variant="outlined"
              color="error"
              size="small"
              onClick={() => setConfirmRemoveOpen(true)}
              disabled={remove.isPending}
            >
              {t('template.remove')}
            </Button>
          </Box>
        </Box>
      ) : (
        <Box sx={styles.noTemplate}>
          <Typography variant="body2" color="text.secondary">
            {t('template.noTemplate')}
          </Typography>
          <Button
            variant="outlined"
            size="small"
            onClick={() => fileInputRef.current?.click()}
            disabled={upload.isPending}
            sx={styles.uploadButton}
          >
            {upload.isPending ? (
              <CircularProgress size={16} sx={{ mr: 1 }} />
            ) : null}
            {t('template.upload')}
          </Button>
        </Box>
      )}

      {/* Hidden file input — .md only */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".md"
        style={{ display: 'none' }}
        onChange={handleFileChange}
        aria-hidden="true"
      />

      {/* Client-side validation errors */}
      {validationError && (
        <Alert severity="error" sx={styles.alert}>
          {validationError}
        </Alert>
      )}

      {/* Server-side upload errors */}
      {upload.isError && (
        <Alert severity="error" sx={styles.alert}>
          {t('template.uploadFailed')}
        </Alert>
      )}

      {/* Remove errors */}
      {remove.isError && (
        <Alert severity="error" sx={styles.alert}>
          {t('template.removeFailed')}
        </Alert>
      )}

      {/* AC-038: token warnings are non-blocking notices */}
      {tokenWarnings.map((warning) => (
        <Alert key={warning} severity="warning" sx={styles.alert}>
          {t('template.unknownToken', { token: warning })}
        </Alert>
      ))}

      {/* AC-008: Remove confirmation dialog */}
      <Dialog
        open={confirmRemoveOpen}
        onClose={() => setConfirmRemoveOpen(false)}
        aria-labelledby="remove-template-dialog-title"
      >
        <DialogTitle id="remove-template-dialog-title">
          {t('template.removeConfirmTitle')}
        </DialogTitle>
        <DialogContent>
          <Typography>{t('template.removeConfirmBody')}</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmRemoveOpen(false)}>
            {t('template.cancel')}
          </Button>
          <Button color="error" onClick={handleRemoveConfirm}>
            {t('template.removeConfirm')}
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
      root: { display: 'flex', flexDirection: 'column' as const, gap: theme.spacing(1.5) },
      sectionLabel: {
        fontWeight: 600,
        textTransform: 'uppercase' as const,
        letterSpacing: '0.07em',
        color: theme.palette.text.secondary,
        fontSize: theme.typography.caption.fontSize,
        marginBottom: theme.spacing(0.5),
      },
      existingTemplate: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(2),
        padding: theme.spacing(1.5),
        borderRadius: 1,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.action.hover,
      },
      fileName: { flexGrow: 1, wordBreak: 'break-all' as const },
      actions: { display: 'flex', gap: theme.spacing(1) },
      noTemplate: { display: 'flex', alignItems: 'center', gap: theme.spacing(2) },
      uploadButton: { flexShrink: 0 },
      alert: { mt: theme.spacing(0.5) },
    }),
    [theme],
  );
