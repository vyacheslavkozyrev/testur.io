'use client';

import { useCallback, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import ADOConnectionForm from '@/components/Integrations/ADOConnectionForm/ADOConnectionForm';
import IntegrationStatusCard from '@/components/Integrations/IntegrationStatusCard/IntegrationStatusCard';
import JiraConnectionForm from '@/components/Integrations/JiraConnectionForm/JiraConnectionForm';
import RemoveIntegrationDialog from '@/components/Integrations/RemoveIntegrationDialog/RemoveIntegrationDialog';
import TestConnectionButton from '@/components/Integrations/TestConnectionButton/TestConnectionButton';
import WebhookSetupPanel from '@/components/Integrations/WebhookSetupPanel/WebhookSetupPanel';
import {
  useIntegrationStatus,
  useSaveADOConnection,
  useSaveJiraConnection,
  useTestConnection,
  useRemoveConnection,
  useWebhookSetup,
  useRegenerateWebhookSecret,
  useUpdateToken,
} from '@/hooks/usePMToolConnection';
import type { PMToolType, SaveADOConnectionRequest, SaveJiraConnectionRequest, UpdateTokenRequest } from '@/types/pmTool.types';

type FormMode = 'none' | 'add-ado' | 'add-jira' | 'update-token';

export interface IntegrationPageProps {
  /** When true, suppresses the standalone page wrapper (title, outer padding). */
  embedded?: boolean;
}

export default function IntegrationPage({ embedded = false }: IntegrationPageProps) {
  const { projectId } = useParams<{ projectId: string }>();
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [formMode, setFormMode] = useState<FormMode>('none');
  const [selectedTool, setSelectedTool] = useState<PMToolType | null>(null);
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);
  const [updateTokenValue, setUpdateTokenValue] = useState('');
  const [updateEmailValue, setUpdateEmailValue] = useState('');

  const { data: integration, isPending, isError } = useIntegrationStatus(projectId ?? '');
  const isConfigured = integration?.pmTool !== null;

  const { data: webhookSetup, isPending: isWebhookPending } = useWebhookSetup(
    projectId ?? '',
    isConfigured,
  );

  const saveADO = useSaveADOConnection(projectId ?? '');
  const saveJira = useSaveJiraConnection(projectId ?? '');
  const testConnection = useTestConnection(projectId ?? '');
  const removeConnection = useRemoveConnection(projectId ?? '');
  const regenerateWebhook = useRegenerateWebhookSecret(projectId ?? '');
  const updateToken = useUpdateToken(projectId ?? '');

  const handleAddADO = useCallback(() => { setSelectedTool('ado'); setFormMode('add-ado'); }, []);
  const handleAddJira = useCallback(() => { setSelectedTool('jira'); setFormMode('add-jira'); }, []);
  const handleCancelForm = useCallback(() => { setFormMode('none'); setSelectedTool(null); }, []);

  const handleSaveADO = useCallback(
    (data: SaveADOConnectionRequest) => {
      saveADO.mutate(data, { onSuccess: () => setFormMode('none') });
    },
    [saveADO],
  );

  const handleSaveJira = useCallback(
    (data: SaveJiraConnectionRequest) => {
      saveJira.mutate(data, { onSuccess: () => setFormMode('none') });
    },
    [saveJira],
  );

  const handleTestConnection = useCallback(() => { testConnection.mutate(); }, [testConnection]);

  const handleRemoveConfirm = useCallback(() => {
    removeConnection.mutate(undefined, { onSuccess: () => setRemoveDialogOpen(false) });
  }, [removeConnection]);

  const handleRemoveCancel = useCallback(() => { setRemoveDialogOpen(false); }, []);
  const handleRegenerate = useCallback(() => { regenerateWebhook.mutate(); }, [regenerateWebhook]);
  const handleShowUpdateToken = useCallback(() => { setFormMode('update-token'); }, []);

  const handleUpdateToken = useCallback(() => {
    const request: UpdateTokenRequest = {
      token: updateTokenValue,
      email: updateEmailValue || undefined,
    };
    updateToken.mutate(request, {
      onSuccess: () => { setFormMode('none'); setUpdateTokenValue(''); setUpdateEmailValue(''); },
    });
  }, [updateToken, updateTokenValue, updateEmailValue]);

  if (isPending) {
    return (
      <Box sx={styles.centered}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !integration) {
    return <Alert severity="error">{t('page.loadError')}</Alert>;
  }

  const cards = (
    <Box sx={styles.cards}>
      {/* Card 1 — Integration status + connection form */}
      <Paper variant="outlined" sx={styles.card}>
        <Box sx={styles.cardContent}>
          <IntegrationStatusCard
            integration={integration}
            onAddConnection={() => setFormMode(selectedTool === 'jira' ? 'add-jira' : 'add-ado')}
            onShowUpdateToken={handleShowUpdateToken}
          />

          {!isConfigured && formMode === 'none' && (
            <>
              <Divider />
              <Box sx={styles.toolSelector}>
                <Typography variant="body2" color="text.secondary">
                  {t('page.selectTool')}
                </Typography>
                <Box sx={styles.toolButtons}>
                  <Button variant="outlined" onClick={handleAddADO}>
                    {t('page.connectAdo')}
                  </Button>
                  <Button variant="outlined" onClick={handleAddJira}>
                    {t('page.connectJira')}
                  </Button>
                </Box>
              </Box>
            </>
          )}

          {formMode === 'add-ado' && (
            <>
              <Divider />
              {saveADO.isError && <Alert severity="error">{t('page.saveError')}</Alert>}
              <ADOConnectionForm
                isSubmitting={saveADO.isPending}
                onSubmit={handleSaveADO}
                onCancel={handleCancelForm}
              />
            </>
          )}

          {formMode === 'add-jira' && (
            <>
              <Divider />
              {saveJira.isError && <Alert severity="error">{t('page.saveError')}</Alert>}
              <JiraConnectionForm
                isSubmitting={saveJira.isPending}
                onSubmit={handleSaveJira}
                onCancel={handleCancelForm}
              />
            </>
          )}

          {isConfigured && formMode === 'none' && (
            <>
              <Divider />
              <TestConnectionButton
                isLoading={testConnection.isPending}
                result={testConnection.data ?? null}
                onTest={handleTestConnection}
              />
            </>
          )}
        </Box>
      </Paper>

      {/* Card 2 — Webhook setup (configured only) */}
      {isConfigured && webhookSetup && !isWebhookPending && formMode === 'none' && (
        <Paper variant="outlined" sx={styles.card}>
          <Box sx={styles.cardContent}>
            <WebhookSetupPanel
              pmTool={integration.pmTool!}
              setup={webhookSetup}
              isRegenerating={regenerateWebhook.isPending}
              onRegenerate={handleRegenerate}
            />
          </Box>
        </Paper>
      )}

      {/* Card 3 — Danger Zone (configured only) */}
      {isConfigured && formMode === 'none' && (
        <Paper variant="outlined" sx={styles.dangerCard}>
          <Box sx={styles.dangerRow}>
            <Box>
              <Typography variant="subtitle2" sx={styles.dangerTitle}>
                {t('page.dangerZone')}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t('page.dangerZoneDescription')}
              </Typography>
            </Box>
            <Button
              variant="outlined"
              color="error"
              size="small"
              onClick={() => setRemoveDialogOpen(true)}
              disabled={removeConnection.isPending}
              sx={styles.dangerButton}
            >
              {t('remove.button')}
            </Button>
          </Box>
        </Paper>
      )}
    </Box>
  );

  return (
    <>
      {!embedded && (
        <Box sx={styles.pageRoot}>
          <Typography variant="h5" sx={styles.pageTitle}>
            {t('page.title')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('page.description')}
          </Typography>
          {cards}
        </Box>
      )}
      {embedded && cards}

      <RemoveIntegrationDialog
        open={removeDialogOpen}
        isRemoving={removeConnection.isPending}
        onConfirm={handleRemoveConfirm}
        onCancel={handleRemoveCancel}
      />
    </>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      pageRoot: {
        padding: theme.spacing(4),
        maxWidth: 860,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      pageTitle: {
        ...theme.typography.h5,
        color: theme.palette.text.primary,
        fontWeight: 600,
      },
      cards: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      card: {
        borderRadius: 1,
      },
      cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(3),
        padding: theme.spacing(3),
      },
      centered: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(4),
      },
      toolSelector: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      toolButtons: {
        display: 'flex',
        gap: theme.spacing(2),
      },
      dangerCard: {
        borderRadius: 1,
        borderColor: theme.palette.error.light,
        padding: theme.spacing(3),
      },
      dangerRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: theme.spacing(2),
      },
      dangerTitle: {
        ...theme.typography.subtitle2,
        fontWeight: 600,
        color: theme.palette.error.main,
        marginBottom: theme.spacing(0.5),
      },
      dangerButton: {
        flexShrink: 0,
      },
    }),
    [theme],
  );
