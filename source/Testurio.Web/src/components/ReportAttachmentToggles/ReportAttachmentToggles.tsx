'use client';

import { useCallback, useMemo, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';

export interface ReportAttachmentTogglesProps {
  /** Current test_type value for this project ("api" | "ui_e2e" | "both"). */
  testType: string;
  includeLogs: boolean;
  includeScreenshots: boolean;
  onChange: (values: { includeLogs: boolean; includeScreenshots: boolean }) => void;
}

export default function ReportAttachmentToggles({
  testType,
  includeLogs,
  includeScreenshots,
  onChange,
}: ReportAttachmentTogglesProps) {
  const { t } = useTranslation('reportSettings');
  const theme = useTheme();
  const styles = getStyles(theme);

  // AC-023, AC-024: screenshots toggle is disabled and coerced to OFF for api-only projects.
  const isApiOnly = testType === 'api';

  // AC-024: coerce includeScreenshots to false when test_type switches to api.
  useEffect(() => {
    if (isApiOnly && includeScreenshots) {
      onChange({ includeLogs, includeScreenshots: false });
    }
  }, [isApiOnly, includeLogs, includeScreenshots, onChange]);

  const handleLogsChange = useCallback(
    (_e: React.ChangeEvent<HTMLInputElement>, checked: boolean) => {
      onChange({ includeLogs: checked, includeScreenshots });
    },
    [includeScreenshots, onChange],
  );

  const handleScreenshotsChange = useCallback(
    (_e: React.ChangeEvent<HTMLInputElement>, checked: boolean) => {
      onChange({ includeLogs, includeScreenshots: checked });
    },
    [includeLogs, onChange],
  );

  return (
    <Box sx={styles.root}>
      <Typography variant="subtitle1" sx={styles.sectionTitle}>
        {t('attachments.title')}
      </Typography>

      <FormControlLabel
        control={
          <Switch checked={includeLogs} onChange={handleLogsChange} />
        }
        label={t('attachments.includeLogs')}
      />

      <Tooltip
        title={isApiOnly ? t('attachments.screenshotsDisabledTooltip') : ''}
        placement="right"
      >
        <span>
          <FormControlLabel
            control={
              <Switch
                checked={isApiOnly ? false : includeScreenshots}
                onChange={handleScreenshotsChange}
                disabled={isApiOnly}
              />
            }
            label={t('attachments.includeScreenshots')}
            disabled={isApiOnly}
          />
        </span>
      </Tooltip>
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(1),
      },
      sectionTitle: { fontWeight: 600 },
    }),
    [theme],
  );
