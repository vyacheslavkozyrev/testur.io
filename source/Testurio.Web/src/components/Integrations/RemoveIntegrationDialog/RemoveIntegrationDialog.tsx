'use client';

import { useTranslation } from 'react-i18next';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';

export interface RemoveIntegrationDialogProps {
  open: boolean;
  isRemoving: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function RemoveIntegrationDialog({
  open,
  isRemoving,
  onConfirm,
  onCancel,
}: RemoveIntegrationDialogProps) {
  const { t } = useTranslation('pmTool');

  return (
    <Dialog open={open} onClose={onCancel} maxWidth="sm" fullWidth>
      <DialogTitle>{t('remove.dialogTitle')}</DialogTitle>
      <DialogContent>
        <DialogContentText>{t('remove.warning')}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isRemoving}>
          {t('common.cancel')}
        </Button>
        <Button
          onClick={onConfirm}
          color="error"
          variant="contained"
          disabled={isRemoving}
          startIcon={isRemoving ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {t('remove.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
