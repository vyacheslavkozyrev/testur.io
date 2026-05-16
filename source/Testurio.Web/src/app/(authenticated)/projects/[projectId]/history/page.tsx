import ProjectHistoryPage from '@/views/ProjectHistoryPage/ProjectHistoryPage';

interface PageProps {
  params: Promise<{ projectId: string }>;
}

export default async function Page({ params }: PageProps) {
  const { projectId } = await params;
  return <ProjectHistoryPage projectId={projectId} />;
}
