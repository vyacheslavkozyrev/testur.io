'use client';

import { useMemo } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import AutoFixHighIcon from '@mui/icons-material/AutoFixHigh';
import BoltIcon from '@mui/icons-material/Bolt';
import ApiIcon from '@mui/icons-material/Api';
import WebIcon from '@mui/icons-material/Web';
import IntegrationInstructionsIcon from '@mui/icons-material/IntegrationInstructions';
import PsychologyIcon from '@mui/icons-material/Psychology';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';

const FEATURES = [
  { icon: AutoFixHighIcon, key: 'aiGeneration' },
  { icon: BoltIcon, key: 'autoTrigger' },
  { icon: ApiIcon, key: 'apiTesting' },
  { icon: WebIcon, key: 'uiE2e' },
  { icon: IntegrationInstructionsIcon, key: 'pmIntegration' },
  { icon: PsychologyIcon, key: 'memoryLayer' },
] as const;

export default function FeaturesSection() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box sx={styles.root}>
      <Container maxWidth="lg">
        <Typography variant="h3" sx={styles.sectionTitle}>
          {t('features.title')}
        </Typography>
        <Grid container spacing={4}>
          {FEATURES.map(({ icon: Icon, key }) => (
            <Grid key={key} size={{ xs: 12, sm: 6, md: 4 }}>
              <Box sx={styles.featureTile}>
                <Box sx={styles.iconWrapper}>
                  <Icon sx={styles.icon} />
                </Box>
                <Typography variant="h6" sx={styles.featureTitle}>
                  {t(`features.${key}.title`)}
                </Typography>
                <Typography sx={styles.featureDesc}>
                  {t(`features.${key}.description`)}
                </Typography>
              </Box>
            </Grid>
          ))}
        </Grid>
      </Container>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        py: { xs: theme.spacing(8), md: theme.spacing(12) },
        backgroundColor: '#ffffff',
      },
      sectionTitle: {
        ...theme.typography.h4,
        fontWeight: 700,
        textAlign: 'center',
        mb: theme.spacing(6),
        color: theme.palette.text.primary,
      },
      featureTile: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1.5),
        p: theme.spacing(3),
        borderRadius: theme.shape.borderRadius,
        border: `1px solid ${theme.palette.divider}`,
        height: '100%',
        '&:hover': {
          borderColor: theme.palette.primary.light,
          boxShadow: `0 4px 16px ${theme.palette.primary.main}18`,
        },
        transition: 'border-color 0.2s, box-shadow 0.2s',
      },
      iconWrapper: {
        width: 44,
        height: 44,
        borderRadius: theme.shape.borderRadius,
        backgroundColor: `${theme.palette.primary.main}14`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      },
      icon: {
        color: theme.palette.primary.main,
        fontSize: 24,
      },
      featureTitle: {
        ...theme.typography.h6,
        fontWeight: 600,
        color: theme.palette.text.primary,
      },
      featureDesc: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        lineHeight: 1.6,
      },
    }),
    [theme],
  );
