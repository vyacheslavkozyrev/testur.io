'use client';

import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import FormControl from '@mui/material/FormControl';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormHelperText from '@mui/material/FormHelperText';
import MenuItem from '@mui/material/MenuItem';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import Select from '@mui/material/Select';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useProjectApiAuth, useUpdateProjectApiAuth } from '@/hooks/useProjectApiAuth';
import type { ApiAuthMethod, ApiAuthApiKeyPlacement, UpdateProjectApiAuthRequest } from '@/types/projectApiAuth.types';

export interface ApiAuthMethodSelectorHandle {
  isDirty: boolean;
  save(): Promise<void>;
}

interface ApiAuthMethodSelectorProps {
  projectId: string;
}

const ApiAuthMethodSelector = forwardRef<ApiAuthMethodSelectorHandle, ApiAuthMethodSelectorProps>(
  function ApiAuthMethodSelector({ projectId }, ref) {
    const { t } = useTranslation('projectApiAuth');
    const theme = useTheme();
    const styles = getStyles(theme);

    const { data: auth, isPending, isError } = useProjectApiAuth(projectId);
    const updateAuth = useUpdateProjectApiAuth(projectId);

    const [selectedMethod, setSelectedMethod] = useState<ApiAuthMethod>('none');
    const [bearerToken, setBearerToken] = useState('');
    const [apiKeyName, setApiKeyName] = useState('');
    const [apiKeyPlacement, setApiKeyPlacement] = useState<ApiAuthApiKeyPlacement>('header');
    const [apiKeyValue, setApiKeyValue] = useState('');
    const [basicUsername, setBasicUsername] = useState('');
    const [basicPassword, setBasicPassword] = useState('');
    const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

    const initializedRef = useRef(false);
    useEffect(() => {
      if (auth && !initializedRef.current) {
        initializedRef.current = true;
        setSelectedMethod(auth.apiAuthMethod);
        setApiKeyName(auth.apiAuthApiKeyName ?? '');
        setApiKeyPlacement(auth.apiAuthApiKeyPlacement ?? 'header');
        setBasicUsername(auth.apiAuthBasicUsername ?? '');
      }
    }, [auth]);

    const handleMethodChange = useCallback((_: React.ChangeEvent<HTMLInputElement>, value: string) => {
      setSelectedMethod(value as ApiAuthMethod);
      setValidationErrors({});
    }, []);

    const validate = useCallback((): boolean => {
      const errors: Record<string, string> = {};
      const sameMethod = auth?.apiAuthMethod === selectedMethod;

      if (selectedMethod === 'bearer') {
        if (!bearerToken.trim() && !sameMethod)
          errors.bearerToken = t('validation.tokenRequired');
      }

      if (selectedMethod === 'api_key') {
        if (!apiKeyName.trim()) errors.apiKeyName = t('validation.keyNameRequired');
        if (!apiKeyValue.trim() && !sameMethod) errors.apiKeyValue = t('validation.keyValueRequired');
      }

      if (selectedMethod === 'basic') {
        if (!basicUsername.trim()) errors.basicUsername = t('validation.usernameRequired');
        if (!basicPassword.trim() && !sameMethod) errors.basicPassword = t('validation.passwordRequired');
      }

      setValidationErrors(errors);
      return Object.keys(errors).length === 0;
    }, [selectedMethod, bearerToken, apiKeyName, apiKeyValue, basicUsername, basicPassword, auth, t]);

    const isDirty = useMemo(() => {
      if (!auth) return false;
      if (selectedMethod !== auth.apiAuthMethod) return true;
      if (selectedMethod === 'bearer' && bearerToken !== '') return true;
      if (selectedMethod === 'api_key') {
        if (apiKeyName !== (auth.apiAuthApiKeyName ?? '')) return true;
        if (apiKeyPlacement !== (auth.apiAuthApiKeyPlacement ?? 'header')) return true;
        if (apiKeyValue !== '') return true;
      }
      if (selectedMethod === 'basic') {
        if (basicUsername !== (auth.apiAuthBasicUsername ?? '')) return true;
        if (basicPassword !== '') return true;
      }
      return false;
    }, [auth, selectedMethod, bearerToken, apiKeyName, apiKeyPlacement, apiKeyValue, basicUsername, basicPassword]);

    useImperativeHandle(ref, () => ({
      get isDirty() { return isDirty; },
      save: async () => {
        if (!validate()) throw new Error('Validation failed');

        let request: UpdateProjectApiAuthRequest;

        if (selectedMethod === 'bearer') {
          request = {
            apiAuthMethod: 'bearer',
            ...(bearerToken ? { apiAuthBearerToken: bearerToken } : {}),
          };
        } else if (selectedMethod === 'api_key') {
          request = {
            apiAuthMethod: 'api_key',
            apiAuthApiKeyName: apiKeyName,
            apiAuthApiKeyPlacement: apiKeyPlacement,
            ...(apiKeyValue ? { apiAuthApiKeyValue: apiKeyValue } : {}),
          };
        } else if (selectedMethod === 'basic') {
          request = {
            apiAuthMethod: 'basic',
            apiAuthBasicUsername: basicUsername,
            ...(basicPassword ? { apiAuthBasicPassword: basicPassword } : {}),
          };
        } else {
          request = { apiAuthMethod: 'none' };
        }

        await updateAuth.mutateAsync(request);
        setBearerToken('');
        setApiKeyValue('');
        setBasicPassword('');
      },
    }), [isDirty, validate, selectedMethod, bearerToken, apiKeyName, apiKeyPlacement, apiKeyValue, basicUsername, basicPassword, updateAuth]);

    if (isPending) return null;
    if (isError) return <Alert severity="error">{t('loadError')}</Alert>;

    const hasExistingBearer = auth?.apiAuthMethod === 'bearer' && auth.apiAuthBearerTokenConfigured;
    const hasExistingApiKeyValue = auth?.apiAuthMethod === 'api_key' && auth.apiAuthApiKeyValueConfigured;
    const hasExistingBasicPassword = auth?.apiAuthMethod === 'basic' && auth.apiAuthBasicPasswordConfigured;

    return (
      <Box sx={styles.root}>
        <FormControl component="fieldset">
          <RadioGroup value={selectedMethod} onChange={handleMethodChange} aria-label={t('methodLabel')}>
            {/* None */}
            <FormControlLabel value="none" control={<Radio />} label={t('methods.none.label')} />

            {/* Bearer Token */}
            <FormControlLabel value="bearer" control={<Radio />} label={t('methods.bearer.label')} />
            {selectedMethod === 'bearer' && (
              <Box sx={styles.credentialFields}>
                <TextField
                  label={t('methods.bearer.tokenLabel')}
                  type="password"
                  value={bearerToken}
                  onChange={(e) => setBearerToken(e.target.value)}
                  error={Boolean(validationErrors.bearerToken)}
                  helperText={
                    validationErrors.bearerToken ??
                    (hasExistingBearer && !bearerToken ? t('methods.bearer.tokenStoredHint') : undefined)
                  }
                  placeholder={hasExistingBearer ? '••••••••' : undefined}
                  size="small"
                  fullWidth
                  required
                />
              </Box>
            )}

            {/* API Key */}
            <FormControlLabel value="api_key" control={<Radio />} label={t('methods.apiKey.label')} />
            {selectedMethod === 'api_key' && (
              <Box sx={styles.credentialFields}>
                <TextField
                  label={t('methods.apiKey.keyNameLabel')}
                  value={apiKeyName}
                  onChange={(e) => setApiKeyName(e.target.value)}
                  error={Boolean(validationErrors.apiKeyName)}
                  helperText={validationErrors.apiKeyName ?? t('methods.apiKey.keyNameHint')}
                  size="small"
                  fullWidth
                  required
                />
                <FormControl size="small" fullWidth>
                  <Typography variant="caption" color="text.secondary" sx={styles.selectLabel}>
                    {t('methods.apiKey.placementLabel')}
                  </Typography>
                  <Select
                    value={apiKeyPlacement}
                    onChange={(e) => setApiKeyPlacement(e.target.value as ApiAuthApiKeyPlacement)}
                  >
                    <MenuItem value="header">{t('methods.apiKey.placementHeader')}</MenuItem>
                    <MenuItem value="query">{t('methods.apiKey.placementQuery')}</MenuItem>
                  </Select>
                  <FormHelperText>{t('methods.apiKey.placementHint')}</FormHelperText>
                </FormControl>
                <TextField
                  label={t('methods.apiKey.keyValueLabel')}
                  type="password"
                  value={apiKeyValue}
                  onChange={(e) => setApiKeyValue(e.target.value)}
                  error={Boolean(validationErrors.apiKeyValue)}
                  helperText={
                    validationErrors.apiKeyValue ??
                    (hasExistingApiKeyValue && !apiKeyValue ? t('methods.apiKey.keyValueStoredHint') : undefined)
                  }
                  placeholder={hasExistingApiKeyValue ? '••••••••' : undefined}
                  size="small"
                  fullWidth
                  required
                />
              </Box>
            )}

            {/* HTTP Basic Auth */}
            <FormControlLabel value="basic" control={<Radio />} label={t('methods.basic.label')} />
            {selectedMethod === 'basic' && (
              <Box sx={styles.credentialFields}>
                <TextField
                  label={t('methods.basic.usernameLabel')}
                  value={basicUsername}
                  onChange={(e) => setBasicUsername(e.target.value)}
                  error={Boolean(validationErrors.basicUsername)}
                  helperText={validationErrors.basicUsername}
                  size="small"
                  fullWidth
                  required
                />
                <TextField
                  label={t('methods.basic.passwordLabel')}
                  type="password"
                  value={basicPassword}
                  onChange={(e) => setBasicPassword(e.target.value)}
                  error={Boolean(validationErrors.basicPassword)}
                  helperText={
                    validationErrors.basicPassword ??
                    (hasExistingBasicPassword && !basicPassword ? t('methods.basic.passwordStoredHint') : undefined)
                  }
                  placeholder={hasExistingBasicPassword ? '••••••••' : undefined}
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

export default ApiAuthMethodSelector;

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
      credentialFields: {
        marginLeft: theme.spacing(4),
        marginBottom: theme.spacing(1),
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        maxWidth: 400,
      },
      selectLabel: {
        marginBottom: theme.spacing(0.5),
      },
    }),
    [theme],
  );
