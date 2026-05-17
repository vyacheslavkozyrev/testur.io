'use client';

import { useMemo } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';

const STEPS = ['connectPm', 'moveStory', 'aiReads', 'testsRun', 'resultsPosted'] as const;

export default function HowItWorksSection() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box id="how-it-works" sx={styles.root}>
      <Container maxWidth="lg">
        <Typography variant="h3" sx={styles.sectionTitle}>
          {t('howItWorks.title')}
        </Typography>
        <Box sx={styles.timeline}>
          {STEPS.map((key, index) => (
            <Box key={key} sx={styles.step}>
              {/* Step number circle */}
              <Box sx={styles.stepNumberWrapper}>
                <Box sx={styles.stepNumber}>
                  <Typography sx={styles.stepNumberText}>{index + 1}</Typography>
                </Box>
                {/* Connector line — visible for all steps except the last */}
                {index < STEPS.length - 1 && <Box sx={styles.connector} />}
              </Box>
              {/* Step content */}
              <Box sx={styles.stepContent}>
                <Typography variant="h6" sx={styles.stepTitle}>
                  {t(`howItWorks.steps.${key}.title`)}
                </Typography>
                <Typography sx={styles.stepDesc}>
                  {t(`howItWorks.steps.${key}.description`)}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>
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
        backgroundColor: theme.palette.background.default,
      },
      sectionTitle: {
        ...theme.typography.h4,
        fontWeight: 700,
        textAlign: 'center',
        mb: theme.spacing(6),
        color: theme.palette.text.primary,
      },
      timeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: 0,
        maxWidth: 700,
        mx: 'auto',
      },
      step: {
        display: 'flex',
        gap: theme.spacing(3),
        alignItems: 'flex-start',
      },
      stepNumberWrapper: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        flexShrink: 0,
      },
      stepNumber: {
        width: 44,
        height: 44,
        borderRadius: '50%',
        backgroundColor: theme.palette.primary.main,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
      },
      stepNumberText: {
        ...theme.typography.body1,
        fontWeight: 700,
        color: '#ffffff',
      },
      connector: {
        width: 2,
        flex: 1,
        minHeight: 32,
        backgroundColor: theme.palette.divider,
        my: theme.spacing(0.5),
      },
      stepContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(0.5),
        pb: theme.spacing(4),
      },
      stepTitle: {
        ...theme.typography.h6,
        fontWeight: 600,
        color: theme.palette.text.primary,
      },
      stepDesc: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        lineHeight: 1.6,
      },
    }),
    [theme],
  );
