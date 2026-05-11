import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';

export interface ProjectDeleteDialogProps {
  open: boolean;
  projectName: string;
  isDeleting: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function ProjectDeleteDialog({
  open,
  projectName,
  isDeleting,
  onConfirm,
  onCancel,
}: ProjectDeleteDialogProps) {
  const { t } = useTranslation('project');

  const handleConfirm = useCallback(() => {
    onConfirm();
  }, [onConfirm]);

  const handleCancel = useCallback(() => {
    onCancel();
  }, [onCancel]);

  return (
    <Dialog open={open} onClose={isDeleting ? undefined : handleCancel} maxWidth="xs" fullWidth>
      <DialogTitle>{t('deleteDialog.title')}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {t('deleteDialog.message', { name: projectName })}
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleCancel} disabled={isDeleting}>
          {t('deleteDialog.actions.cancel')}
        </Button>
        <Button
          onClick={handleConfirm}
          color="error"
          variant="contained"
          disabled={isDeleting}
          startIcon={isDeleting ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {t('deleteDialog.actions.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
