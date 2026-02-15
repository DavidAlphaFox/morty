/**
 * API 客户端模块
 * 负责与后端 REST API 进行通信
 * 包含项目、故事、迭代等数据的 CRUD 操作
 */

import type { Project, Story, Iteration, Plan, CreateStoryDto, UpdateStoryDto, UpdateRequirementsDto, UpdateAcceptanceCriteriaDto, StartPhaseDto } from '../types';

const BASE = '/api';

/**
 * 处理 API 响应
 * @param res Fetch Response 对象
 * @returns 解析后的 JSON 数据
 * @throws 如果响应不 OK 则抛出错误
 */
async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
  return res.json();
}

/**
 * 获取所有项目列表
 * @returns 项目数组
 */
export async function fetchProjects(): Promise<Project[]> {
  const res = await fetch(`${BASE}/projects`);
  return json<Project[]>(res);
}

/**
 * 获取指定项目的所有故事（用户故事）
 * @param projectId 项目 ID
 * @returns 属于该项目的故事数组
 */
export async function fetchProjectStories(projectId: number): Promise<Story[]> {
  const res = await fetch(`${BASE}/projects/${projectId}/stories`);
  return json<Story[]>(res);
}

/**
 * 创建新的用户故事
 * @param dto 包含项目 ID、故事 ID、标题、优先级的数据传输对象
 * @returns 创建成功的故事对象
 */
export async function createStory(dto: CreateStoryDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

/**
 * 更新现有故事的信息
 * @param id 故事 ID
 * @param dto 包含要更新的字段（标题、优先级、状态）的对象
 * @returns 更新后的故事对象
 */
export async function updateStory(id: number, dto: UpdateStoryDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

/**
 * 获取故事的迭代历史
 * @param storyId 故事 ID
 * @returns 该故事的所有迭代记录数组
 */
export async function fetchStoryIterations(storyId: number): Promise<Iteration[]> {
  const res = await fetch(`${BASE}/stories/${storyId}/iterations`);
  return json<Iteration[]>(res);
}

/**
 * 获取故事的计划内容
 * @param storyId 故事 ID
 * @returns 计划对象，如果不存在则返回 null
 */
export async function fetchStoryPlan(storyId: number): Promise<Plan | null> {
  const res = await fetch(`${BASE}/stories/${storyId}/plan`);
  if (res.status === 404) return null;
  return json<Plan>(res);
}

/**
 * 更新故事的需求
 * @param id 故事 ID
 * @param dto 需求数据
 * @returns 更新后的故事
 */
export async function updateStoryRequirements(id: number, dto: UpdateRequirementsDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/requirements`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

/**
 * 更新故事的验收标准
 * @param id 故事 ID
 * @param dto 验收标准数据
 * @returns 更新后的故事
 */
export async function updateStoryAcceptanceCriteria(id: number, dto: UpdateAcceptanceCriteriaDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/acceptance`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}

/**
 * 开始故事的指定阶段
 * @param id 故事 ID
 * @param dto 阶段数据
 * @returns 更新后的故事
 */
export async function startStoryPhase(id: number, dto: StartPhaseDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/start-phase`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Story>(res);
}
