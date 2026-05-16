'use client';

import { useCallback, useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import InputLabel from '@mui/material/InputLabel';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import type { JiraAuthMethod, SaveJiraConnectionRequest } from '@/types/pmTool.types';

export interface JiraConnectionFormProps {
  isSubmitting: boolean;
  onSubmit: (data: SaveJiraConnectionRequest) => void;
  onCancel?: () => void;
}

interface FormValues {
  baseUrl: string;
  projectKey: string;
  inTestingStatus: string;
  authMethod: JiraAuthMethod;
  email: string;
  apiToken: string;
  pat: string;
}

export default function JiraConnectionForm({
  isSubmitting,
  onSubmit,
  onCancel,
}: JiraConnectionFormProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  const {
    control,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<FormValues>({
    defaultValues: {
      baseUrl: '',
      projectKey: '',
      inTestingStatus: '',
      authMethod: 'apiToken',
      email: '',
      apiToken: '',
      pat: '',
    },
  });

  const authMethod = watch('authMethod');

  const handleFormSubmit = useCallback(
    (data: FormValues) => {
      const request: SaveJiraConnectionRequest = {
        baseUrl: data.baseUrl,
        projectKey: data.projectKey,
        inTestingStatus: data.inTestingStatus,
        authMethod: data.authMethod,
        ...(data.authMethod === 'apiToken'
          ? { email: data.email, apiToken: data.apiToken }
          : { pat: data.pat }),
      };
      onSubmit(request);
    },
    [onSubmit],
  );

  return (
    <Box component="form" onSubmit={handleSubmit(handleFormSubmit)} sx={styles.root} noValidate>
      <Typography variant="subtitle1" sx={styles.title}>
        {t('jira.formTitle')}
      </Typography>

      <Controller
        name="baseUrl"
        control={control}
        rules={{
          required: t('jira.validation.baseUrlRequired'),
          pattern: { value: /^https?:\/\/.+/, message: t('jira.validation.baseUrlInvalid') },
        }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('jira.fields.baseUrl')}
            type="url"
            error={Boolean(errors.baseUrl)}
            helperText={errors.baseUrl?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="projectKey"
        control={control}
        rules={{ required: t('jira.validation.projectKeyRequired') }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('jira.fields.projectKey')}
            error={Boolean(errors.projectKey)}
            helperText={errors.projectKey?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="inTestingStatus"
        control={control}
        rules={{ required: t('jira.validation.inTestingStatusRequired') }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('jira.fields.inTestingStatus')}
            error={Boolean(errors.inTestingStatus)}
            helperText={errors.inTestingStatus?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="authMethod"
        control={control}
        rules={{ required: true }}
        render={({ field }) => (
          <FormControl fullWidth required error={Boolean(errors.authMethod)}>
            <InputLabel id="jira-auth-method-label">{t('jira.fields.authMethod')}</InputLabel>
            <Select {...field} labelId="jira-auth-method-label" label={t('jira.fields.authMethod')}>
              <MenuItem value="apiToken">{t('jira.authMethods.apiToken')}</MenuItem>
              <MenuItem value="pat">{t('jira.authMethods.pat')}</MenuItem>
            </Select>
            {errors.authMethod && (
              <FormHelperText>{t('jira.validation.authMethodRequired')}</FormHelperText>
            )}
          </FormControl>
        )}
      />

      {authMethod === 'apiToken' && (
        <>
          <Controller
            name="email"
            control={control}
            rules={{
              required: t('jira.validation.emailRequired'),
              pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: t('jira.validation.emailInvalid') },
            }}
            render={({ field }) => (
              <TextField
                {...field}
                label={t('jira.fields.email')}
                type="email"
                error={Boolean(errors.email)}
                helperText={errors.email?.message}
                fullWidth
                required
              />
            )}
          />

          <Controller
            name="apiToken"
            control={control}
            rules={{ required: t('jira.validation.apiTokenRequired') }}
            render={({ field }) => (
              <TextField
                {...field}
                label={t('jira.fields.apiToken')}
                type="password"
                error={Boolean(errors.apiToken)}
                helperText={errors.apiToken?.message}
                fullWidth
                required
              />
            )}
          />
        </>
      )}

      {authMethod === 'pat' && (
        <Controller
          name="pat"
          control={control}
          rules={{ required: t('jira.validation.patRequired') }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('jira.fields.pat')}
              type="password"
              error={Boolean(errors.pat)}
              helperText={errors.pat?.message}
              fullWidth
              required
            />
          )}
        />
      )}

      <Box sx={styles.actions}>
        {onCancel && (
          <Button variant="outlined" onClick={onCancel} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
        )}
        <Button
          type="submit"
          variant="contained"
          disabled={isSubmitting}
          startIcon={isSubmitting ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {t('common.save')}
        </Button>
      </Box>
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
        gap: theme.spacing(3),
        maxWidth: 640,
        width: '100%',
      },
      title: {
        ...theme.typography.subtitle1,
        color: theme.palette.text.primary,
      },
      actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: theme.spacing(2),
        marginTop: theme.spacing(1),
      },
    }),
    [theme],
  );
