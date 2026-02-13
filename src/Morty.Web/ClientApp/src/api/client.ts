import type { Project, Story, Iteration, Plan, CreateStoryDto, UpdateStoryDto } from '../types';

const BASE = '/api';

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
  return res.json();
}

export async function fetchProjects(): Promise<Project[]> {
  const res = await fetch(`${BASE}/projects`);
  return json<Project[]>(res);
}

export async function fetchProjectStories(projectId: number): Promise<Story[]> {
  const res = await fetch(`${BASE}/projects/${projectId}/stories`);
  return json<Story[]>(res);
}

export async function createStory(dto: CreateStoryDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

export async function updateStory(id: number, dto: UpdateStoryDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

export async function fetchStoryIterations(storyId: number): Promise<Iteration[]> {
  const res = await fetch(`${BASE}/stories/${storyId}/iterations`);
  return json<Iteration[]>(res);
}

export async function fetchStoryPlan(storyId: number): Promise<Plan | null> {
  const res = await fetch(`${BASE}/stories/${storyId}/plan`);
  if (res.status === 404) return null;
  return json<Plan>(res);
}
