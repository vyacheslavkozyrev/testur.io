'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Typography from '@mui/material/Typography';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import StarIcon from '@mui/icons-material/Star';
import { alpha, useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { PlanDefinition, BillingInterval } from '@/types/plan.types';
import { SIGN_UP_ROUTE } from '@/routes/routes';

export interface PlanCardProps {
  plan: PlanDefinition;
  interval: BillingInterval;
  isAuthenticated: boolean;
  planRank: number;
  currentPlanRank: number | null;
}

function getMonthlyEquivalentPrice(plan: PlanDefinition, interval: BillingInterval): number {
  if (interval === 'monthly') return plan.monthlyPrice;
  // Annual total ÷ 12, rounded to nearest dollar
  return Math.round(plan.annualPrice / 12);
}

export default function PlanCard({ plan, interval, isAuthenticated, planRank, currentPlanRank }: PlanCardProps) {
  const { t } = useTranslation('pricing');
  const theme = useTheme();

  const isCurrent = currentPlanRank !== null && planRank === currentPlanRank;
  const isDowngrade = currentPlanRank !== null && planRank < currentPlanRank;
  const ctaDisabled = isCurrent || isDowngrade;

  const styles = getStyles(theme, plan.isPopular);

  const displayPrice = getMonthlyEquivalentPrice(plan, interval);

  const ctaHref = isAuthenticated
    ? `/billing?plan=${plan.id}&interval=${interval}`
    : `${SIGN_UP_ROUTE}?plan=${plan.id}&interval=${interval}`;

  const ctaLabel = isCurrent
    ? t('planCard.currentPlan')
    : isDowngrade
      ? t('planCard.downgrade')
      : isAuthenticated
        ? t('planCard.upgrade')
        : t('planCard.getStarted');

  return (
    <Box sx={styles.root}>
      {/* Most popular badge */}
      {plan.isPopular && (
        <Box sx={styles.popularBadgeWrapper}>
          <Chip
            icon={<StarIcon sx={{ fontSize: 14, color: '#fff !important' }} />}
            label={t('planCard.mostPopular')}
            size="small"
            sx={styles.popularBadge}
          />
        </Box>
      )}

      <Box sx={styles.inner}>
        {/* Plan name */}
        <Typography variant="h5" sx={styles.planName}>
          {plan.name}
        </Typography>

        {/* Price */}
        <Box sx={styles.priceRow}>
          {displayPrice === 0 ? (
            <Typography sx={styles.freePrice}>
              {t('planCard.free')}
            </Typography>
          ) : (
            <>
              <Typography sx={styles.priceAmount}>
                ${displayPrice}
              </Typography>
              <Typography sx={styles.priceUnit}>
                {t('planCard.perMonth')}
              </Typography>
            </>
          )}
        </Box>

        {/* Annual discount badge */}
        {interval === 'annual' && plan.annualDiscountPercent > 0 && (
          <Chip
            label={t('planCard.annualDiscount', { percent: plan.annualDiscountPercent })}
            size="small"
            color="success"
            sx={styles.discountBadge}
          />
        )}

        <Divider sx={styles.divider} />

        {/* Feature checklist */}
        <List dense disablePadding sx={styles.featureList}>
          {plan.features.map((feature) => (
            <ListItem key={feature} disablePadding sx={styles.featureItem}>
              <ListItemIcon sx={styles.featureIcon}>
                <CheckCircleOutlineIcon sx={{ fontSize: 18, color: theme.palette.success.main }} />
              </ListItemIcon>
              <ListItemText
                primary={feature}
                primaryTypographyProps={{ sx: styles.featureText }}
              />
            </ListItem>
          ))}
        </List>

        {/* CTA */}
        <Button
          component={ctaDisabled ? 'button' : Link}
          href={ctaDisabled ? undefined : ctaHref}
          variant={plan.isPopular && !ctaDisabled ? 'contained' : 'outlined'}
          fullWidth
          disabled={ctaDisabled}
          sx={styles.ctaButton}
        >
          {ctaLabel}
        </Button>
      </Box>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme, isPopular: boolean) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        position: 'relative' as const,
        height: '100%',
        borderRadius: '10px',
        border: isPopular
          ? `2px solid ${theme.palette.primary.main}`
          : `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper,
        boxShadow: isPopular
          ? `0 8px 32px ${alpha(theme.palette.primary.main, 0.157)}`
          : '0 2px 8px rgba(0,0,0,0.06)',
        overflow: 'visible',
        display: 'flex',
        flexDirection: 'column',
      },
      popularBadgeWrapper: {
        position: 'absolute' as const,
        top: -14,
        left: '50%',
        transform: 'translateX(-50%)',
        zIndex: 1,
      },
      popularBadge: {
        backgroundColor: theme.palette.primary.main,
        color: '#ffffff',
        fontWeight: 700,
        fontSize: '0.75rem',
        px: theme.spacing(0.5),
      },
      inner: {
        p: theme.spacing(3),
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        height: '100%',
      },
      planName: {
        fontWeight: 700,
        color: theme.palette.text.primary,
      },
      priceRow: {
        display: 'flex',
        alignItems: 'baseline',
        gap: theme.spacing(0.5),
      },
      freePrice: {
        fontWeight: 800,
        color: theme.palette.text.primary,
      },
      priceAmount: {
        fontWeight: 800,
        color: theme.palette.primary.main,
      },
      priceUnit: {
        color: theme.palette.text.secondary,
      },
      discountBadge: {
        alignSelf: 'flex-start',
        fontWeight: 600,
      },
      divider: {
        my: theme.spacing(0.5),
      },
      featureList: {
        flex: 1,
      },
      featureItem: {
        py: theme.spacing(0.25),
      },
      featureIcon: {
        minWidth: 28,
      },
      featureText: {
        color: theme.palette.text.secondary,
      },
      ctaButton: {
        minHeight: 44,
        fontWeight: 600,
        mt: 'auto',
      },
    }),
    [theme, isPopular],
  );
