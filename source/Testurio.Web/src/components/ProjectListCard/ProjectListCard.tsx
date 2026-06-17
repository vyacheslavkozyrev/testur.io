'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { ProjectDto } from '@/types/project.types';
import { PROJECT_HISTORY_ROUTE, PROJECT_SETTINGS_ROUTE } from '@/routes/routes';
import { truncateText } from '@/utils/truncateText';

const STRATEGY_MAX_LENGTH = 120;

export interface ProjectListCardProps {
  project: ProjectDto;
}

export default function ProjectListCard({ project }: ProjectListCardProps) {
  const { t } = useTranslation('projects');
  const theme = useTheme();
  const styles = getStyles(theme);

  const truncatedStrategy = useMemo(
    () => truncateText(project.testingStrategy, STRATEGY_MAX_LENGTH),
    [project.testingStrategy],
  );

  return (
    <Card sx={styles.card}>
      {/* Main clickable area — navigates to project history */}
      <Box
        component={Link}
        href={PROJECT_HISTORY_ROUTE(project.projectId)}
        sx={styles.cardLink}
      >
        <CardContent sx={styles.content}>
          <Box sx={styles.header}>
            <Typography variant="h6" sx={styles.name} noWrap>
              {project.name}
            </Typography>
            {/* Spacer that reserves space for the absolutely positioned edit button */}
            <Box sx={styles.editSpacer} />
          </Box>

          <Typography variant="body2" sx={styles.url} noWrap>
            {project.productUrl}
          </Typography>

          <Typography variant="body2" sx={styles.strategy}>
            {truncatedStrategy}
          </Typography>
        </CardContent>
      </Box>

      {/* Edit button — absolutely positioned sibling of the main link to avoid nested <a> */}
      <IconButton
        component={Link}
        href={PROJECT_SETTINGS_ROUTE(project.projectId)}
        size="small"
        aria-label={t('card.editAriaLabel')}
        sx={styles.editButton}
      >
        <EditOutlinedIcon fontSize="small" />
      </IconButton>
    </Card>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) => ({
  card: {
    height: '100%',
    display: 'flex',
    flexDirection: 'column' as const,
    border: `1px solid ${theme.palette.divider}`,
    borderRadius: `${theme.shape.borderRadius}px`,
    transition: 'box-shadow 150ms ease',
    position: 'relative' as const,
    '&:hover': {
      boxShadow: theme.shadows[4],
    },
  },
  cardLink: {
    display: 'block',
    height: '100%',
    textDecoration: 'none',
    color: 'inherit',
    '&:hover': {
      textDecoration: 'none',
    },
  },
  content: {
    display: 'flex',
    flexDirection: 'column' as const,
    gap: theme.spacing(1),
    width: '100%',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing(1),
    minWidth: 0,
  },
  name: {
    color: theme.palette.text.primary,
    flex: 1,
    minWidth: 0,
  },
  editSpacer: {
    width: 28,
    flexShrink: 0,
  },
  editButton: {
    position: 'absolute' as const,
    top: theme.spacing(1),
    right: theme.spacing(1),
    color: theme.palette.text.secondary,
    '&:hover': {
      color: theme.palette.primary.main,
      backgroundColor: theme.palette.action.hover,
    },
  },
  url: {
    color: theme.palette.text.secondary,
  },
  strategy: {
    color: theme.palette.text.primary,
  },
});
