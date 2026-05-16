'use client';

import { useMemo, type ReactNode } from 'react';
import Link from 'next/link';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Tooltip from '@mui/material/Tooltip';
import { useTheme, type Theme } from '@mui/material/styles';

export interface NavItemProps {
  icon: ReactNode;
  label: string;
  href: string;
  active: boolean;
  collapsed: boolean;
  tooltip?: string;
}

export default function NavItem({ icon, label, href, active, collapsed, tooltip }: NavItemProps) {
  const theme = useTheme();
  const styles = getStyles(theme, active);

  const button = (
    <ListItemButton
      component={Link}
      href={href}
      selected={active}
      sx={styles.button}
      aria-label={collapsed ? (tooltip ?? label) : undefined}
    >
      <ListItemIcon sx={styles.icon}>{icon}</ListItemIcon>
      {!collapsed && (
        <ListItemText
          primary={label}
          primaryTypographyProps={{ sx: styles.label }}
          sx={{ m: 0 }}
        />
      )}
    </ListItemButton>
  );

  if (collapsed) {
    return (
      <Tooltip title={tooltip ?? label} placement="right">
        {button}
      </Tooltip>
    );
  }

  return button;
}

// co-located at the bottom of the file
const getStyles = (theme: Theme, active: boolean) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      button: {
        borderRadius: `${theme.shape.borderRadius}px`,
        px: 2,
        py: 1,
        mb: 0.5,
        backgroundColor: active ? theme.palette.primary.main : 'transparent',
        color: active ? theme.palette.primary.contrastText : theme.palette.text.primary,
        '&:hover': {
          backgroundColor: active
            ? theme.palette.primary.dark
            : theme.palette.action.hover,
        },
        '&.Mui-selected': {
          backgroundColor: theme.palette.primary.main,
          color: theme.palette.primary.contrastText,
          '&:hover': {
            backgroundColor: theme.palette.primary.dark,
          },
        },
        '&:focus-visible': {
          outline: `2px solid ${theme.palette.primary.main}`,
          outlineOffset: 2,
        },
      },
      icon: {
        color: 'inherit',
        minWidth: 36,
      },
      label: {
        ...theme.typography.body2,
        fontWeight: active ? 600 : 400,
        color: 'inherit',
      },
    }),
    [theme, active],
  );
