/**
 * API 客户端模块
 * 负责与后端 REST API 进行通信
 * 包含项目、故事、迭代等数据的 CRUD 操作
 */

import type { Project, Story, Iteration, Plan, RoundSummary, CreateStoryDto, UpdateStoryDto, UpdateRequirementsDto, UpdateUserAcceptanceCriteriaDto, StartPhaseDto, CreateProjectDto, UpdateProjectDto, EnvConfigGroup, EnvConfigRule, CreateEnvConfigGroupDto, UpdateEnvConfigGroupDto, CreateEnvConfigRuleDto, UpdateEnvConfigRuleDto } from '../types';

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
 * 获取指定项目详情
 * @param id 项目 ID
 * @returns 项目对象
 */
export async function fetchProject(id: number): Promise<Project> {
  const res = await fetch(`${BASE}/projects/${id}`);
  return json<Project>(res);
}

/**
 * 创建新项目
 * @param dto 包含名称、工作目录、PRD的数据传输对象
 * @returns 创建成功的项目对象
 */
export async function createProject(dto: CreateProjectDto): Promise<Project> {
  const res = await fetch(`${BASE}/projects`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Project>(res);
}

/**
 * 更新现有项目的信息
 * @param id 项目 ID
 * @param dto 包含要更新的字段的对象
 * @returns 更新后的项目对象
 */
export async function updateProject(id: number, dto: UpdateProjectDto): Promise<Project> {
  const res = await fetch(`${BASE}/projects/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<Project>(res);
}

/**
 * 删除项目
 * @param id 项目 ID
 */
export async function deleteProject(id: number): Promise<void> {
  const res = await fetch(`${BASE}/projects/${id}`, {
    method: 'DELETE',
  });
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
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
 * 获取指定故事详情
 * @param id 故事 ID
 * @returns 故事对象
 */
export async function fetchStory(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}`);
  return json<Story>(res);
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
 * 更新故事的验收标准（用户原始输入）
 * @param id 故事 ID
 * @param dto 验收标准数据
 * @returns 更新后的故事
 */
export async function updateUserAcceptanceCriteria(id: number, dto: UpdateUserAcceptanceCriteriaDto): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/user-acceptance`, {
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

/**
 * 启动故事（暂停 -> 运行）
 * @param id 故事 ID
 * @returns 更新后的故事
 */
export async function startStory(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/start`, {
    method: 'POST',
  });
  return json<Story>(res);
}

/**
 * 暂停故事（运行 -> 暂停）
 * @param id 故事 ID
 * @returns 更新后的故事
 */
export async function pauseStory(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/pause`, {
    method: 'POST',
  });
  return json<Story>(res);
}

/**
 * 重新调度故事（从Completed/Failed回到Pending）
 * @param id 故事 ID
 * @returns 更新后的故事
 */
export async function rescheduleStory(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/reschedule`, {
    method: 'POST',
  });
  return json<Story>(res);
}

/**
 * 获取故事的轮次摘要
 * @param storyId 故事 ID
 * @returns 轮次摘要数组
 */
export async function fetchStoryRounds(storyId: number): Promise<RoundSummary[]> {
  const res = await fetch(`${BASE}/stories/${storyId}/rounds`);
  return json<RoundSummary[]>(res);
}

/**
 * 获取故事指定轮次的迭代
 * @param storyId 故事 ID
 * @param round 轮次
 * @returns 迭代数组
 */
export async function fetchStoryIterationsByRound(storyId: number, round: number): Promise<Iteration[]> {
  const res = await fetch(`${BASE}/stories/${storyId}/iterations?round=${round}`);
  return json<Iteration[]>(res);
}

/**
 * 重新生成详细实施计划
 * @param id 故事 ID
 * @returns 更新后的故事
 */
export async function regeneratePlan(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/regenerate-plan`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });
  return json<Story>(res);
}

/**
 * 重新生成验收标准
 * @param id 故事 ID
 * @returns 更新后的故事
 */
export async function regenerateAcceptance(id: number): Promise<Story> {
  const res = await fetch(`${BASE}/stories/${id}/regenerate-acceptance`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });
  return json<Story>(res);
}

// ==================== 环境配置组 API ====================

/**
 * 获取所有环境配置组
 * @returns 环境配置组数组
 */
export async function fetchEnvConfigGroups(): Promise<EnvConfigGroup[]> {
  const res = await fetch(`${BASE}/envconfiggroups`);
  return json<EnvConfigGroup[]>(res);
}

/**
 * 获取指定环境配置组
 * @param id 环境配置组 ID
 * @returns 环境配置组对象
 */
export async function fetchEnvConfigGroup(id: number): Promise<EnvConfigGroup> {
  const res = await fetch(`${BASE}/envconfiggroups/${id}`);
  return json<EnvConfigGroup>(res);
}

/**
 * 创建新环境配置组
 * @param dto 创建数据
 * @returns 创建成功的环境配置组对象
 */
export async function createEnvConfigGroup(dto: CreateEnvConfigGroupDto): Promise<EnvConfigGroup> {
  const res = await fetch(`${BASE}/envconfiggroups`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<EnvConfigGroup>(res);
}

/**
 * 更新环境配置组
 * @param id 环境配置组 ID
 * @param dto 更新数据
 * @returns 更新后的环境配置组对象
 */
export async function updateEnvConfigGroup(id: number, dto: UpdateEnvConfigGroupDto): Promise<EnvConfigGroup> {
  const res = await fetch(`${BASE}/envconfiggroups/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<EnvConfigGroup>(res);
}

/**
 * 删除环境配置组
 * @param id 环境配置组 ID
 */
export async function deleteEnvConfigGroup(id: number): Promise<void> {
  const res = await fetch(`${BASE}/envconfiggroups/${id}`, {
    method: 'DELETE',
  });
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
}

// ==================== 环境变量规则 API ====================

/**
 * 获取项目的所有环境变量规则
 * @param projectId 项目 ID
 * @returns 规则数组
 */
export async function fetchEnvConfigRules(projectId: number): Promise<EnvConfigRule[]> {
  const res = await fetch(`${BASE}/projects/${projectId}/envconfigrules`);
  return json<EnvConfigRule[]>(res);
}

/**
 * 获取指定环境变量规则
 * @param projectId 项目 ID
 * @param id 规则 ID
 * @returns 规则对象
 */
export async function fetchEnvConfigRule(projectId: number, id: number): Promise<EnvConfigRule> {
  const res = await fetch(`${BASE}/projects/${projectId}/envconfigrules/${id}`);
  return json<EnvConfigRule>(res);
}

/**
 * 创建新环境变量规则
 * @param projectId 项目 ID
 * @param dto 创建数据
 * @returns 创建成功的规则对象
 */
export async function createEnvConfigRule(projectId: number, dto: CreateEnvConfigRuleDto): Promise<EnvConfigRule> {
  const res = await fetch(`${BASE}/projects/${projectId}/envconfigrules`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<EnvConfigRule>(res);
}

/**
 * 更新环境变量规则
 * @param projectId 项目 ID
 * @param id 规则 ID
 * @param dto 更新数据
 * @returns 更新后的规则对象
 */
export async function updateEnvConfigRule(projectId: number, id: number, dto: UpdateEnvConfigRuleDto): Promise<EnvConfigRule> {
  const res = await fetch(`${BASE}/projects/${projectId}/envconfigrules/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return json<EnvConfigRule>(res);
}

/**
 * 删除环境变量规则
 * @param projectId 项目 ID
 * @param id 规则 ID
 */
export async function deleteEnvConfigRule(projectId: number, id: number): Promise<void> {
  const res = await fetch(`${BASE}/projects/${projectId}/envconfigrules/${id}`, {
    method: 'DELETE',
  });
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
}
