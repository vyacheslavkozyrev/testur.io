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
import type { ADOAuthMethod, SaveADOConnectionRequest } from '@/types/pmTool.types';

export interface ADOConnectionFormProps {
  isSubmitting: boolean;
  onSubmit: (data: SaveADOConnectionRequest) => void;
  onCancel?: () => void;
}

interface FormValues {
  orgUrl: string;
  projectName: string;
  team: string;
  inTestingStatus: string;
  authMethod: ADOAuthMethod;
  pat: string;
  oAuthToken: string;
}

export default function ADOConnectionForm({
  isSubmitting,
  onSubmit,
  onCancel,
}: ADOConnectionFormProps) {
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
      orgUrl: '',
      projectName: '',
      team: '',
      inTestingStatus: '',
      authMethod: 'pat',
      pat: '',
      oAuthToken: '',
    },
  });

  const authMethod = watch('authMethod');

  const handleFormSubmit = useCallback(
    (data: FormValues) => {
      const request: SaveADOConnectionRequest = {
        orgUrl: data.orgUrl,
        projectName: data.projectName,
        team: data.team,
        inTestingStatus: data.inTestingStatus,
        authMethod: data.authMethod,
        ...(data.authMethod === 'pat' ? { pat: data.pat } : { oAuthToken: data.oAuthToken }),
      };
      onSubmit(request);
    },
    [onSubmit],
  );

  return (
    <Box component="form" onSubmit={handleSubmit(handleFormSubmit)} sx={styles.root} noValidate>
      <Typography variant="h6" sx={styles.title}>
        {t('ado.formTitle')}
      </Typography>

      <Controller
        name="orgUrl"
        control={control}
        rules={{
          required: t('ado.validation.orgUrlRequired'),
          pattern: { value: /^https?:\/\/.+/, message: t('ado.validation.orgUrlInvalid') },
        }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('ado.fields.orgUrl')}
            type="url"
            error={Boolean(errors.orgUrl)}
            helperText={errors.orgUrl?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="projectName"
        control={control}
        rules={{ required: t('ado.validation.projectNameRequired') }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('ado.fields.projectName')}
            error={Boolean(errors.projectName)}
            helperText={errors.projectName?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="team"
        control={control}
        rules={{ required: t('ado.validation.teamRequired') }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('ado.fields.team')}
            error={Boolean(errors.team)}
            helperText={errors.team?.message}
            fullWidth
            required
          />
        )}
      />

      <Controller
        name="inTestingStatus"
        control={control}
        rules={{ required: t('ado.validation.inTestingStatusRequired') }}
        render={({ field }) => (
          <TextField
            {...field}
            label={t('ado.fields.inTestingStatus')}
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
            <InputLabel id="ado-auth-method-label">{t('ado.fields.authMethod')}</InputLabel>
            <Select {...field} labelId="ado-auth-method-label" label={t('ado.fields.authMethod')}>
              <MenuItem value="pat">{t('ado.authMethods.pat')}</MenuItem>
              <MenuItem value="oAuth">{t('ado.authMethods.oAuth')}</MenuItem>
            </Select>
            {errors.authMethod && (
              <FormHelperText>{t('ado.validation.authMethodRequired')}</FormHelperText>
            )}
          </FormControl>
        )}
      />

      {authMethod === 'pat' && (
        <Controller
          name="pat"
          control={control}
          rules={{ required: t('ado.validation.patRequired') }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('ado.fields.pat')}
              type="password"
              error={Boolean(errors.pat)}
              helperText={errors.pat?.message}
              fullWidth
              required
            />
          )}
        />
      )}

      {authMethod === 'oAuth' && (
        <Controller
          name="oAuthToken"
          control={control}
          rules={{ required: t('ado.validation.oAuthTokenRequired') }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('ado.fields.oAuthToken')}
              type="password"
              error={Boolean(errors.oAuthToken)}
              helperText={errors.oAuthToken?.message}
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
        ...theme.typography.h6,
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
