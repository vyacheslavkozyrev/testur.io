export interface ProjectDto {
  projectId: string;
  name: string;
  productUrl: string;
  testingStrategy: string;
  customPrompt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProjectRequest {
  name: string;
  productUrl: string;
  testingStrategy: string;
  customPrompt?: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  productUrl: string;
  testingStrategy: string;
  customPrompt?: string | null;
}

export interface PromptCheckRequest {
  customPrompt: string;
}

export interface PromptCheckDimension {
  assessment: string;
  suggestion: string | null;
}

export interface PromptCheckFeedback {
  clarity: PromptCheckDimension;
  specificity: PromptCheckDimension;
  potentialConflicts: PromptCheckDimension;
}
