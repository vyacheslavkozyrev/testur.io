'use client';

import { useCallback, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
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
  /** When true, suppresses the outer page container (padding, maxWidth) for tab-embedded use. */
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
  const webhookEnabled = isConfigured;

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

  const handleAddADO = useCallback(() => {
    setSelectedTool('ado');
    setFormMode('add-ado');
  }, []);

  const handleAddJira = useCallback(() => {
    setSelectedTool('jira');
    setFormMode('add-jira');
  }, []);

  const handleCancelForm = useCallback(() => {
    setFormMode('none');
    setSelectedTool(null);
  }, []);

  const handleSaveADO = useCallback(
    (data: SaveADOConnectionRequest) => {
      saveADO.mutate(data, {
        onSuccess: () => setFormMode('none'),
      });
    },
    [saveADO],
  );

  const handleSaveJira = useCallback(
    (data: SaveJiraConnectionRequest) => {
      saveJira.mutate(data, {
        onSuccess: () => setFormMode('none'),
      });
    },
    [saveJira],
  );

  const handleTestConnection = useCallback(() => {
    testConnection.mutate();
  }, [testConnection]);

  const handleRemoveConfirm = useCallback(() => {
    removeConnection.mutate(undefined, {
      onSuccess: () => setRemoveDialogOpen(false),
    });
  }, [removeConnection]);

  const handleRemoveCancel = useCallback(() => {
    setRemoveDialogOpen(false);
  }, []);

  const handleRegenerate = useCallback(() => {
    regenerateWebhook.mutate();
  }, [regenerateWebhook]);

  const handleShowUpdateToken = useCallback(() => {
    setFormMode('update-token');
  }, []);

  const handleUpdateToken = useCallback(() => {
    const request: UpdateTokenRequest = {
      token: updateTokenValue,
      email: updateEmailValue || undefined,
    };
    updateToken.mutate(request, {
      onSuccess: () => {
        setFormMode('none');
        setUpdateTokenValue('');
        setUpdateEmailValue('');
      },
    });
  }, [updateToken, updateTokenValue, updateEmailValue]);

  if (isPending) {
    return (
      <Box sx={embedded ? styles.centeredEmbedded : styles.centered}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !integration) {
    return (
      <Box sx={embedded ? undefined : styles.root}>
        <Alert severity="error">{t('page.loadError')}</Alert>
      </Box>
    );
  }

  const content = (
    <>
      {!embedded && (
        <Typography variant="body2" color="text.secondary">
          {t('page.description')}
        </Typography>
      )}

      <IntegrationStatusCard
        integration={integration}
        onAddConnection={() => setFormMode(selectedTool === 'jira' ? 'add-jira' : 'add-ado')}
        onShowUpdateToken={handleShowUpdateToken}
      />

      {!isConfigured && formMode === 'none' && (
        <Box sx={styles.toolSelector}>
          <Typography variant="subtitle2" color="text.secondary">
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
      )}

      {formMode === 'add-ado' && (
        <>
          <Divider />
          {saveADO.isError && (
            <Alert severity="error">{t('page.saveError')}</Alert>
          )}
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
          {saveJira.isError && (
            <Alert severity="error">{t('page.saveError')}</Alert>
          )}
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

          {webhookSetup && !isWebhookPending && (
            <>
              <Divider />
              <WebhookSetupPanel
                pmTool={integration.pmTool!}
                setup={webhookSetup}
                isRegenerating={regenerateWebhook.isPending}
                onRegenerate={handleRegenerate}
              />
            </>
          )}

          <Divider />
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
        </>
      )}

      <RemoveIntegrationDialog
        open={removeDialogOpen}
        isRemoving={removeConnection.isPending}
        onConfirm={handleRemoveConfirm}
        onCancel={handleRemoveCancel}
      />
    </>
  );

  if (embedded) {
    return <Box sx={styles.embeddedContent}>{content}</Box>;
  }

  return (
    <Box sx={styles.root}>
      <Typography variant="h5" sx={styles.pageTitle}>
        {t('page.title')}
      </Typography>
      {content}
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        padding: theme.spacing(4),
        maxWidth: 800,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(3),
      },
      embeddedContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(3),
      },
      centered: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(8),
      },
      centeredEmbedded: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(4),
      },
      pageTitle: {
        ...theme.typography.h5,
        color: theme.palette.text.primary,
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
      dangerRow: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: theme.spacing(2),
        paddingTop: theme.spacing(1),
      },
      dangerTitle: {
        ...theme.typography.subtitle2,
        fontWeight: 600,
        color: theme.palette.error.main,
        marginBottom: theme.spacing(0.5),
      },
      dangerButton: {
        flexShrink: 0,
        alignSelf: 'center',
      },
    }),
    [theme],
  );
