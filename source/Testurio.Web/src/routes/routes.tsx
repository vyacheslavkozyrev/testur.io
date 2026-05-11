import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';

const ProjectSettingsPage = lazy(
  () => import('@/pages/ProjectSettingsPage/ProjectSettingsPage'),
);

function PageLoader() {
  return (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
      <CircularProgress />
    </Box>
  );
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Navigate to="/projects" replace />,
  },
  {
    path: '/projects/:projectId/settings',
    element: (
      <Suspense fallback={<PageLoader />}>
        <ProjectSettingsPage />
      </Suspense>
    ),
  },
]);
