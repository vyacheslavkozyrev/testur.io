'use client';

import { useCallback, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
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
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const truncatedStrategy = useMemo(
    () => truncateText(project.testingStrategy, STRATEGY_MAX_LENGTH),
    [project.testingStrategy],
  );

  const handleEditClick = useCallback(
    (e: React.MouseEvent<HTMLButtonElement>) => {
      e.stopPropagation();
      e.preventDefault();
      router.push(PROJECT_SETTINGS_ROUTE(project.projectId));
    },
    [router, project.projectId],
  );

  return (
    <Card sx={styles.card}>
      <CardActionArea
        component={Link}
        href={PROJECT_HISTORY_ROUTE(project.projectId)}
        sx={styles.actionArea}
      >
        <CardContent sx={styles.content}>
          <Box sx={styles.header}>
            <Typography variant="h6" sx={styles.name} noWrap>
              {project.name}
            </Typography>
            <IconButton
              size="small"
              aria-label={t('card.editAriaLabel')}
              onClick={handleEditClick}
              sx={styles.editButton}
            >
              <EditOutlinedIcon fontSize="small" />
            </IconButton>
          </Box>

          <Typography variant="body2" sx={styles.url} noWrap>
            {project.productUrl}
          </Typography>

          <Typography variant="body2" sx={styles.strategy}>
            {truncatedStrategy}
          </Typography>
        </CardContent>
      </CardActionArea>
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
    borderRadius: 1,
    transition: 'box-shadow 150ms ease',
    '&:hover': {
      boxShadow: theme.shadows[4],
    },
  },
  actionArea: {
    height: '100%',
    alignItems: 'flex-start' as const,
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
    ...theme.typography.h6,
    color: theme.palette.text.primary,
    flex: 1,
    minWidth: 0,
  },
  editButton: {
    color: theme.palette.text.secondary,
    flexShrink: 0,
    '&:hover': {
      color: theme.palette.primary.main,
      backgroundColor: theme.palette.action.hover,
    },
  },
  url: {
    ...theme.typography.body2,
    color: theme.palette.text.secondary,
  },
  strategy: {
    ...theme.typography.body2,
    color: theme.palette.text.primary,
  },
});
