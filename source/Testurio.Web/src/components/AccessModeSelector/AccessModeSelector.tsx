'use client';

import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import FormControl from '@mui/material/FormControl';
import FormControlLabel from '@mui/material/FormControlLabel';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useProjectAccess, useUpdateProjectAccess } from '@/hooks/useProjectAccess';
import type { AccessMode, UpdateProjectAccessRequest } from '@/types/projectAccess.types';
import { PUBLISHED_EGRESS_IPS } from '@/config/egressIps';

export interface AccessModeSelectorHandle {
  isDirty: boolean;
  save(): Promise<void>;
}

interface AccessModeSelectorProps {
  projectId: string;
}

const AccessModeSelector = forwardRef<AccessModeSelectorHandle, AccessModeSelectorProps>(
  function AccessModeSelector({ projectId }, ref) {
    const { t } = useTranslation('projectAccess');
    const theme = useTheme();
    const styles = getStyles(theme);

    const { data: access, isPending, isError } = useProjectAccess(projectId);
    const updateAccess = useUpdateProjectAccess(projectId);

    const [selectedMode, setSelectedMode] = useState<AccessMode>('ipAllowlist');
    const [basicAuthUser, setBasicAuthUser] = useState('');
    const [basicAuthPass, setBasicAuthPass] = useState('');
    const [headerTokenName, setHeaderTokenName] = useState('');
    const [headerTokenValue, setHeaderTokenValue] = useState('');
    const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

    const initializedRef = useRef(false);
    useEffect(() => {
      if (access && !initializedRef.current) {
        initializedRef.current = true;
        setSelectedMode(access.accessMode);
        setBasicAuthUser(access.basicAuthUser ?? '');
        setHeaderTokenName(access.headerTokenName ?? '');
      }
    }, [access]);

    const handleModeChange = useCallback((_: React.ChangeEvent<HTMLInputElement>, value: string) => {
      setSelectedMode(value as AccessMode);
      setValidationErrors({});
    }, []);

    const validate = useCallback((): boolean => {
      const errors: Record<string, string> = {};
      const hasExistingCredential = access?.accessMode === selectedMode;

      if (selectedMode === 'basicAuth') {
        if (!basicAuthUser.trim()) errors.basicAuthUser = t('validation.usernameRequired');
        if (!basicAuthPass.trim() && !hasExistingCredential)
          errors.basicAuthPass = t('validation.passwordRequired');
      }

      if (selectedMode === 'headerToken') {
        if (!headerTokenName.trim()) errors.headerTokenName = t('validation.headerNameRequired');
        else if (!/^[A-Za-z0-9\-]+$/.test(headerTokenName))
          errors.headerTokenName = t('validation.headerNameInvalid');
        if (!headerTokenValue.trim() && !hasExistingCredential)
          errors.headerTokenValue = t('validation.headerValueRequired');
      }

      setValidationErrors(errors);
      return Object.keys(errors).length === 0;
    }, [selectedMode, basicAuthUser, basicAuthPass, headerTokenName, headerTokenValue, access, t]);

    const isDirty = useMemo(() => {
      if (!access) return false;
      if (selectedMode !== access.accessMode) return true;
      if (selectedMode === 'basicAuth') {
        if (basicAuthUser !== (access.basicAuthUser ?? '')) return true;
        if (basicAuthPass !== '') return true;
      }
      if (selectedMode === 'headerToken') {
        if (headerTokenName !== (access.headerTokenName ?? '')) return true;
        if (headerTokenValue !== '') return true;
      }
      return false;
    }, [access, selectedMode, basicAuthUser, basicAuthPass, headerTokenName, headerTokenValue]);

    useImperativeHandle(ref, () => ({
      get isDirty() { return isDirty; },
      save: async () => {
        if (!validate()) throw new Error('Validation failed');

        const request: UpdateProjectAccessRequest = {
          accessMode: selectedMode,
          ...(selectedMode === 'basicAuth' && {
            basicAuthUser,
            ...(basicAuthPass ? { basicAuthPass } : {}),
          }),
          ...(selectedMode === 'headerToken' && {
            headerTokenName,
            ...(headerTokenValue ? { headerTokenValue } : {}),
          }),
        };

        await updateAccess.mutateAsync(request);
        setBasicAuthPass('');
        setHeaderTokenValue('');
      },
    }), [isDirty, validate, selectedMode, basicAuthUser, basicAuthPass, headerTokenName, headerTokenValue, updateAccess]);

    if (isPending) return null;
    if (isError) return <Alert severity="error">{t('loadError')}</Alert>;

    return (
      <Box sx={styles.root}>
        <FormControl component="fieldset">
          <RadioGroup
            value={selectedMode}
            onChange={handleModeChange}
            aria-label={t('modeLabel')}
          >
            {/* IP Allowlisting */}
            <FormControlLabel
              value="ipAllowlist"
              control={<Radio />}
              label={t('modes.ipAllowlist.label')}
            />
            {selectedMode === 'ipAllowlist' && (
              <Box sx={styles.ipPanel}>
                <Typography variant="body2" sx={styles.ipDescription}>
                  {t('modes.ipAllowlist.description')}
                </Typography>
                <Typography variant="body2" sx={styles.ipAddresses}>
                  {PUBLISHED_EGRESS_IPS.join(', ')}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('modes.ipAllowlist.setupStep1')}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('modes.ipAllowlist.setupStep2')}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('modes.ipAllowlist.setupStep3')}
                </Typography>
              </Box>
            )}

            {/* HTTP Basic Auth */}
            <FormControlLabel
              value="basicAuth"
              control={<Radio />}
              label={t('modes.basicAuth.label')}
            />
            {selectedMode === 'basicAuth' && (
              <Box sx={styles.credentialFields}>
                <TextField
                  label={t('modes.basicAuth.usernameLabel')}
                  value={basicAuthUser}
                  onChange={(e) => setBasicAuthUser(e.target.value)}
                  error={Boolean(validationErrors.basicAuthUser)}
                  helperText={validationErrors.basicAuthUser}
                  size="small"
                  fullWidth
                  required
                />
                <TextField
                  label={t('modes.basicAuth.passwordLabel')}
                  type="password"
                  value={basicAuthPass}
                  onChange={(e) => setBasicAuthPass(e.target.value)}
                  error={Boolean(validationErrors.basicAuthPass)}
                  helperText={
                    validationErrors.basicAuthPass ??
                    (access?.accessMode === 'basicAuth' && !basicAuthPass
                      ? t('modes.basicAuth.passwordStoredHint')
                      : undefined)
                  }
                  placeholder={access?.accessMode === 'basicAuth' ? '••••••••' : undefined}
                  size="small"
                  fullWidth
                  required
                />
              </Box>
            )}

            {/* Custom Header Token */}
            <FormControlLabel
              value="headerToken"
              control={<Radio />}
              label={t('modes.headerToken.label')}
            />
            {selectedMode === 'headerToken' && (
              <Box sx={styles.credentialFields}>
                <TextField
                  label={t('modes.headerToken.headerNameLabel')}
                  value={headerTokenName}
                  onChange={(e) => setHeaderTokenName(e.target.value)}
                  error={Boolean(validationErrors.headerTokenName)}
                  helperText={
                    validationErrors.headerTokenName ?? t('modes.headerToken.headerNameHint')
                  }
                  size="small"
                  fullWidth
                  required
                />
                <TextField
                  label={t('modes.headerToken.headerValueLabel')}
                  type="password"
                  value={headerTokenValue}
                  onChange={(e) => setHeaderTokenValue(e.target.value)}
                  error={Boolean(validationErrors.headerTokenValue)}
                  helperText={
                    validationErrors.headerTokenValue ??
                    (access?.accessMode === 'headerToken' && !headerTokenValue
                      ? t('modes.headerToken.valueStoredHint')
                      : undefined)
                  }
                  placeholder={access?.accessMode === 'headerToken' ? '••••••••' : undefined}
                  size="small"
                  fullWidth
                  required
                />
              </Box>
            )}
          </RadioGroup>
        </FormControl>
      </Box>
    );
  },
);

export default AccessModeSelector;

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
      ipPanel: {
        marginLeft: theme.spacing(4),
        marginBottom: theme.spacing(1),
        padding: theme.spacing(2),
        backgroundColor: theme.palette.action.hover,
        borderRadius: theme.shape.borderRadius,
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(0.5),
      },
      ipDescription: {
        color: theme.palette.text.secondary,
        marginBottom: theme.spacing(1),
      },
      ipAddresses: {
        fontFamily: 'monospace',
        fontWeight: theme.typography.fontWeightBold,
        color: theme.palette.text.primary,
        marginBottom: theme.spacing(1),
      },
      credentialFields: {
        marginLeft: theme.spacing(4),
        marginBottom: theme.spacing(1),
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        maxWidth: 400,
      },
    }),
    [theme],
  );
