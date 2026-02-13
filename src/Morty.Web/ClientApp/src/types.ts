export type StoryStatus = 'Pending' | 'Planning' | 'InProgress' | 'Verifying' | 'Completed' | 'Failed';

export const STORY_STATUSES: StoryStatus[] = [
  'Pending',
  'Planning',
  'InProgress',
  'Verifying',
  'Completed',
  'Failed',
];

export const COLUMN_CONFIG: Record<StoryStatus, { title: string; color: string }> = {
  Pending:    { title: 'Backlog',     color: '#919eab' },
  Planning:   { title: 'Planning',    color: '#8b5cf6' },
  InProgress: { title: 'In Progress', color: '#3b82f6' },
  Verifying:  { title: 'Verifying',   color: '#f59e0b' },
  Completed:  { title: 'Done',        color: '#22c55e' },
  Failed:     { title: 'Failed',      color: '#ef4444' },
};

export type Priority = 'High' | 'Medium' | 'Low';

export interface Story {
  id: number;
  projectId: number;
  storyId: string;
  title: string;
  priority: Priority;
  status: StoryStatus;
  createdAt: string;
  completedAt: string | null;
}

export interface KanbanColumn {
  id: StoryStatus;
  title: string;
  color: string;
  stories: Story[];
}

export interface Project {
  id: number;
  name: string;
  workingDirectory: string;
  prdJson: string;
  createdAt: string;
}

export interface Iteration {
  id: number;
  storyId: number;
  iterationNum: number;
  startedAt: string;
  completedAt: string | null;
  durationMs: number;
  output: string | null;
}

export interface Plan {
  id: number;
  storyId: number;
  planContent: string;
  createdAt: string;
}

export interface CreateStoryDto {
  projectId: number;
  storyId: string;
  title: string;
  priority: Priority;
}

export interface UpdateStoryDto {
  title?: string;
  priority?: Priority;
  status?: StoryStatus;
}
