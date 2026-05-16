import { forwardRef, useCallback, useImperativeHandle, useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import type { ProjectDto, CreateProjectRequest, UpdateProjectRequest } from '@/types/project.types';

export interface ProjectFormHandle {
  /** Validates the form. Calls onSubmit with data if valid. Resolves true if valid, false otherwise. */
  triggerSubmit: () => Promise<boolean>;
  isDirty: boolean;
}

export interface ProjectFormProps {
  /** Pre-populate fields when editing an existing project. */
  project?: ProjectDto;
  isSubmitting: boolean;
  onSubmit: (data: CreateProjectRequest | UpdateProjectRequest) => void;
  onCancel?: () => void;
}

interface FormValues {
  name: string;
  productUrl: string;
  testingStrategy: string;
}

const ProjectForm = forwardRef<ProjectFormHandle, ProjectFormProps>(
  function ProjectForm({ project, isSubmitting, onSubmit, onCancel }, ref) {
    const { t } = useTranslation('project');
    const theme = useTheme();
    const styles = getStyles(theme);
    const isEdit = Boolean(project);

    const {
      control,
      handleSubmit,
      formState: { errors, isDirty },
    } = useForm<FormValues>({
      defaultValues: {
        name: project?.name ?? '',
        productUrl: project?.productUrl ?? '',
        testingStrategy: project?.testingStrategy ?? '',
      },
    });

    const handleFormSubmit = useCallback(
      (data: FormValues) => {
        onSubmit(data);
      },
      [onSubmit],
    );

    useImperativeHandle(ref, () => ({
      triggerSubmit: () =>
        new Promise<boolean>((resolve) => {
          handleSubmit(
            (data) => {
              handleFormSubmit(data);
              resolve(true);
            },
            () => resolve(false),
          )();
        }),
      get isDirty() {
        return isDirty;
      },
    }));

    return (
      <Box component="form" onSubmit={handleSubmit(handleFormSubmit)} sx={styles.root} noValidate>
        <Typography variant="h6" sx={styles.title}>
          {isEdit ? t('form.titleEdit') : t('form.titleCreate')}
        </Typography>

        <Controller
          name="name"
          control={control}
          rules={{
            required: t('form.validation.nameRequired'),
            maxLength: { value: 200, message: t('form.validation.nameMaxLength') },
          }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('form.fields.name')}
              error={Boolean(errors.name)}
              helperText={errors.name?.message}
              fullWidth
              required
              inputProps={{ maxLength: 201 }}
            />
          )}
        />

        <Controller
          name="productUrl"
          control={control}
          rules={{
            required: t('form.validation.productUrlRequired'),
            pattern: {
              value: /^https?:\/\/.+/,
              message: t('form.validation.productUrlInvalid'),
            },
          }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('form.fields.productUrl')}
              type="url"
              error={Boolean(errors.productUrl)}
              helperText={errors.productUrl?.message}
              fullWidth
              required
            />
          )}
        />

        <Controller
          name="testingStrategy"
          control={control}
          rules={{
            required: t('form.validation.testingStrategyRequired'),
            maxLength: { value: 500, message: t('form.validation.testingStrategyMaxLength') },
          }}
          render={({ field }) => (
            <TextField
              {...field}
              label={t('form.fields.testingStrategy')}
              multiline
              minRows={3}
              error={Boolean(errors.testingStrategy)}
              helperText={errors.testingStrategy?.message}
              fullWidth
              required
              inputProps={{ maxLength: 501 }}
            />
          )}
        />

        {/* In create mode, show inline actions; edit mode uses the page-level SaveBar */}
        {!isEdit && (
          <Box sx={styles.actions}>
            {onCancel && (
              <Button variant="outlined" onClick={onCancel} disabled={isSubmitting}>
                {t('form.actions.cancel')}
              </Button>
            )}
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              {t('form.actions.create')}
            </Button>
          </Box>
        )}
      </Box>
    );
  },
);

export default ProjectForm;

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(3),
        width: '100%',
      },
      title: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
        marginBottom: theme.spacing(1),
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
