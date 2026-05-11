import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';

export default function HomePage() {
  return (
    <Box sx={{ p: 4, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography variant="h4">Welcome to Testurio</Typography>
      <Box>
        <Button variant="contained" component={Link} href="/projects/new">
          Create Project
        </Button>
      </Box>
    </Box>
  );
}
