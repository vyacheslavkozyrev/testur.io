'use client';

import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import type { PMToolConnectionResponse } from '@/types/pmTool.types';

export interface IntegrationStatusCardProps {
  integration: PMToolConnectionResponse;
  onAddConnection: () => void;
  onShowUpdateToken: () => void;
}

export default function IntegrationStatusCard({
  integration,
  onAddConnection,
  onShowUpdateToken,
}: IntegrationStatusCardProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const isConfigured = integration.pmTool !== null;

  if (!isConfigured) {
    return (
      <Box sx={styles.notConfigured}>
        <Typography variant="body1" color="text.secondary">
          {t('status.notConfigured')}
        </Typography>
        <Typography
          component="button"
          onClick={onAddConnection}
          sx={styles.addLink}
          variant="body1"
        >
          {t('status.addConnection')}
        </Typography>
      </Box>
    );
  }

  const toolLabel =
    integration.pmTool === 'ado' ? t('status.toolAdo') : t('status.toolJira');

  const identifier =
    integration.pmTool === 'ado'
      ? `${integration.adoOrgUrl} / ${integration.adoProjectName}`
      : `${integration.jiraBaseUrl} / ${integration.jiraProjectKey}`;

  const authMethodLabel =
    integration.pmTool === 'ado'
      ? integration.adoAuthMethod === 'pat'
        ? t('ado.authMethods.pat')
        : t('ado.authMethods.oAuth')
      : integration.jiraAuthMethod === 'apiToken'
        ? t('jira.authMethods.apiToken')
        : t('jira.authMethods.pat');

  return (
    <Box sx={styles.configured}>
      <Box sx={styles.row}>
        <Typography variant="body1" sx={styles.label}>
          {t('status.tool')}
        </Typography>
        <Chip label={toolLabel} color="primary" size="small" />
      </Box>

      <Box sx={styles.row}>
        <Typography variant="body1" sx={styles.label}>
          {t('status.identifier')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {identifier}
        </Typography>
      </Box>

      <Box sx={styles.row}>
        <Typography variant="body1" sx={styles.label}>
          {t('status.authMethod')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {authMethodLabel}
        </Typography>
      </Box>

      {integration.integrationStatus === 'authError' && (
        <Alert
          severity="error"
          sx={styles.authErrorAlert}
          action={
            <Typography
              component="button"
              onClick={onShowUpdateToken}
              sx={styles.updateTokenLink}
              variant="body2"
            >
              {t('status.updateToken')}
            </Typography>
          }
        >
          {t('status.authErrorMessage')}
        </Alert>
      )}
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      notConfigured: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1),
        padding: theme.spacing(3),
        border: `1px dashed ${theme.palette.divider}`,
        borderRadius: theme.shape.borderRadius,
        alignItems: 'flex-start',
      },
      addLink: {
        cursor: 'pointer',
        color: theme.palette.primary.main,
        background: 'none',
        border: 'none',
        padding: 0,
        textDecoration: 'underline',
      },
      configured: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        padding: theme.spacing(3),
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: theme.shape.borderRadius,
      },
      row: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(2),
      },
      label: {
        ...theme.typography.body1,
        color: theme.palette.text.secondary,
        minWidth: 120,
      },
      authErrorAlert: {
        width: '100%',
        marginTop: theme.spacing(1),
      },
      updateTokenLink: {
        cursor: 'pointer',
        background: 'none',
        border: 'none',
        padding: 0,
        color: 'inherit',
        textDecoration: 'underline',
        whiteSpace: 'nowrap',
      },
    }),
    [theme],
  );
