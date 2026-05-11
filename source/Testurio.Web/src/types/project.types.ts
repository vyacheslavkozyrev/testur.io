export interface ProjectDto {
  projectId: string;
  name: string;
  productUrl: string;
  testingStrategy: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProjectRequest {
  name: string;
  productUrl: string;
  testingStrategy: string;
}

export interface UpdateProjectRequest {
  name: string;
  productUrl: string;
  testingStrategy: string;
}
