/**
 * 类型定义模块
 * 定义前端应用中使用的所有数据类型
 */

/**
 * 故事阶段枚举
 * - Pending: 初始状态
 * - RequirementsPlanning: 计划分析阶段 - 生成详细计划
 * - AcceptancePlanning: 验收标准阶段 - 细化验收标准
 * - Coding: 编码阶段
 * - Testing: 测试阶段
 * - Acceptance: 验收阶段
 * - Completed: 完成
 * - Failed: 失败
 */
export type StoryPhase =
  | 'Pending'
  | 'RequirementsPlanning'
  | 'AcceptancePlanning'
  | 'Coding'
  | 'Testing'
  | 'Acceptance'
  | 'Completed'
  | 'Failed';

/**
 * 用户故事的当前状态
 * - Pending: 待处理，尚未开始
 * - Planning: 正在规划阶段
 * - InProgress: 正在进行中
 * - Verifying: 正在验证/测试
 * - Completed: 已完成
 * - Failed: 失败
 */
export type StoryStatus = 'Pending' | 'Planning' | 'InProgress' | 'Verifying' | 'Completed' | 'Failed';

/**
 * 所有可用的故事状态列表
 * 用于 Kanban 面板的列定义
 */
export const STORY_STATUSES: StoryStatus[] = [
  'Pending',
  'Planning',
  'InProgress',
  'Verifying',
  'Completed',
  'Failed',
];

/**
 * 所有可用的阶段列表
 */
export const STORY_PHASES: StoryPhase[] = [
  'Pending',
  'RequirementsPlanning',
  'AcceptancePlanning',
  'Coding',
  'Testing',
  'Acceptance',
  'Completed',
  'Failed',
];

/**
 * 阶段显示配置
 */
export const PHASE_CONFIG: Record<StoryPhase, { title: string; color: string }> = {
  Pending:              { title: 'Pending',              color: '#919eab' },  // 灰色
  RequirementsPlanning: { title: 'Requirements',        color: '#8b5cf6' },  // 紫色
  AcceptancePlanning:   { title: 'Acceptance',          color: '#a855f7' },  // 紫色
  Coding:              { title: 'Coding',              color: '#3b82f6' },  // 蓝色
  Testing:              { title: 'Testing',             color: '#f59e0b' },  // 橙色
  Acceptance:           { title: 'Acceptance',          color: '#06b6d4' },  // 青色
  Completed:            { title: 'Done',               color: '#22c55e' },  // 绿色
  Failed:               { title: 'Failed',             color: '#ef4444' },  // 红色
};

/**
 * Kanban列ID类型（基于Phase分组，合并为6列）
 */
export type KanbanColumnId = 'Pending' | 'Planning' | 'Coding' | 'Testing' | 'Completed' | 'Failed';

/**
 * 所有Kanban列
 */
export const KANBAN_COLUMNS: KanbanColumnId[] = [
  'Pending', 'Planning', 'Coding', 'Testing', 'Completed', 'Failed'
];

/**
 * Phase到Kanban列的映射
 * 将8个阶段映射到6个Kanban列
 */
export const PHASE_TO_COLUMN: Record<StoryPhase, KanbanColumnId> = {
  Pending: 'Pending',
  RequirementsPlanning: 'Planning',
  AcceptancePlanning: 'Planning',  // 合并到Planning
  Coding: 'Coding',
  Testing: 'Testing',
  Acceptance: 'Testing',           // 验收阶段暂时归入Testing
  Completed: 'Completed',
  Failed: 'Failed',
};

/**
 * Kanban列配置
 */
export const KANBAN_COLUMN_CONFIG: Record<KanbanColumnId, { title: string; color: string }> = {
  Pending:   { title: 'Backlog',   color: '#919eab' },
  Planning:  { title: 'Planning',  color: '#8b5cf6' },
  Coding:    { title: 'Coding',    color: '#3b82f6' },
  Testing:   { title: 'Testing',   color: '#f59e0b' },
  Completed: { title: 'Done',      color: '#22c55e' },
  Failed:    { title: 'Failed',    color: '#ef4444' },
};

/**
 * Kanban列到默认Phase的映射（用于拖拽时设置phase）
 */
export const COLUMN_TO_DEFAULT_PHASE: Record<KanbanColumnId, StoryPhase> = {
  Pending: 'Pending',
  Planning: 'RequirementsPlanning',
  Coding: 'Coding',
  Testing: 'Testing',
  Completed: 'Completed',
  Failed: 'Failed',
};

/**
 * Kanban 面板列配置
 * 定义每个状态对应的显示名称和颜色
 */
export const COLUMN_CONFIG: Record<StoryStatus, { title: string; color: string }> = {
  Pending:    { title: 'Backlog',     color: '#919eab' },   // 灰色 - 待办
  Planning:   { title: 'Planning',    color: '#8b5cf6' },  // 紫色 - 规划中
  InProgress: { title: 'In Progress', color: '#3b82f6' },   // 蓝色 - 进行中
  Verifying:  { title: 'Verifying',   color: '#f59e0b' },   // 橙色 - 验证中
  Completed:  { title: 'Done',        color: '#22c55e' },   // 绿色 - 已完成
  Failed:     { title: 'Failed',      color: '#ef4444' },   // 红色 - 失败
};

/**
 * 故事优先级
 * - High: 高优先级，应该优先处理
 * - Medium: 中等优先级
 * - Low: 低优先级
 */
export type Priority = 'High' | 'Medium' | 'Low';

/**
 * 用户故事实体
 * 代表一个独立的开发任务或需求
 */
export interface Story {
  id: number;              // 数据库唯一标识
  projectId: number;       // 所属项目 ID
  storyId: string;        // 业务层故事编号 (如 "S-ABC123")
  title: string;          // 故事标题/描述
  priority: Priority;     // 优先级
  status: StoryStatus;    // 当前状态
  createdAt: string;     // 创建时间 (ISO 格式)
  completedAt: string | null; // 完成时间 (ISO 格式)，未完成则为 null

  // 调度控制字段
  isPaused: boolean;           // 是否暂停
  source: StorySource;         // 故事来源

  // 多阶段处理相关字段
  phase: StoryPhase;           // 当前阶段
  requirements: string;        // 用户需求（原始 PRD）
  detailedPlan: string;        // 详细实施计划（RequirementsPlanning 阶段 plan mode 输出）
  userAcceptanceCriteria: string; // 用户验收标准（原始）
  acceptanceCriteria: string; // 细化后的验收标准（AcceptancePlanning 阶段 plan mode 输出）
  currentIteration: number;  // 当前阶段内迭代次数

  // 依赖关系
  dependencies: number[];      // 依赖的故事ID列表
}

/**
 * 故事来源
 */
export type StorySource = 'UserAdded' | 'AutoDiscovered';

/**
 * Kanban 面板列
 * 包含特定状态的所有故事
 */
export interface KanbanColumn {
  id: KanbanColumnId;     // 列 ID
  title: string;          // 显示标题
  color: string;          // 颜色代码 (十六进制)
  stories: Story[];       // 该列中的所有故事
}

/**
 * 项目实体
 * 代表一个完整的开发项目
 */
export interface Project {
  id: number;              // 数据库唯一标识
  name: string;           // 项目名称
  workingDirectory: string; // 工作目录路径
  prdJson: string;       // 产品需求文档 (JSON 格式)
  createdAt: string;     // 创建时间 (ISO 格式)
}

/**
 * 迭代实体
 * 代表故事的一次执行/尝试
 * Morty 循环会多次迭代直到任务完成
 */
export interface Iteration {
  id: number;              // 数据库唯一标识
  storyId: number;        // 所属故事 ID
  iterationNum: number;   // 迭代序号 (从 1 开始)
  startedAt: string;      // 开始时间 (ISO 格式)
  completedAt: string | null; // 完成时间 (ISO 格式)
  durationMs: number;     // 持续时间 (毫秒)
  output: string | null; // 迭代输出内容
}

/**
 * 计划实体
 * 包含 AI 生成的实现计划
 */
export interface Plan {
  id: number;              // 数据库唯一标识
  storyId: number;        // 所属故事 ID
  planContent: string;   // 计划内容 (JSON 格式)
  createdAt: string;     // 创建时间 (ISO 格式)
}

/**
 * 创建故事的数据传输对象
 * 用于 POST 请求创建新故事
 */
export interface CreateStoryDto {
  projectId: number;   // 所属项目 ID
  storyId: string;    // 业务层故事编号
  title: string;      // 故事标题
  priority: Priority; // 优先级
  source?: StorySource; // 故事来源（默认 UserAdded）
  requirements?: string; // 用户需求
  userAcceptanceCriteria?: string; // 用户验收标准
  dependencies?: number[]; // 依赖的故事ID列表（父任务）
}

/**
 * 更新故事的数据传输对象
 * 用于 PATCH 请求更新故事信息
 * 所有字段都是可选的
 */
export interface UpdateStoryDto {
  title?: string;      // 新标题
  priority?: Priority; // 新优先级
  status?: StoryStatus; // 新状态
}

/**
 * 更新需求的数据传输对象
 */
export interface UpdateRequirementsDto {
  requirements: string; // 用户需求（原始 PRD）
}

/**
 * 更新用户验收标准的数据传输对象
 */
export interface UpdateUserAcceptanceCriteriaDto {
  userAcceptanceCriteria: string; // 用户验收标准（原始）
}

/**
 * 开始阶段的数据传输对象
 */
export interface StartPhaseDto {
  phase: StoryPhase; // 要开始的阶段
}

/**
 * 创建项目的数据传输对象
 * 用于 POST 请求创建新项目
 * workingDirectory 为空时由后端自动生成
 */
export interface CreateProjectDto {
  name: string;            // 项目名称
  workingDirectory?: string; // 工作目录路径（可选，由后端自动生成）
  prdJson: string;        // 产品需求文档 (JSON 格式)
}

/**
 * 更新项目的数据传输对象
 * 用于 PUT 请求更新项目信息
 * 所有字段都是可选的
 */
export interface UpdateProjectDto {
  name?: string;           // 新名称
  workingDirectory?: string; // 新工作目录
  prdJson?: string;       // 新 PRD
}

/**
 * 环境配置组实体
 * 全局环境变量配置组
 */
export interface EnvConfigGroup {
  id: number;
  name: string;
  description: string;
  createdAt: string;
  variables: EnvVariable[];
}

/**
 * 环境变量实体
 */
export interface EnvVariable {
  id: number;
  envConfigGroupId: number;
  key: string;
  value: string;
  isRequired: boolean;
  defaultValue?: string;
}

/**
 * 环境变量规则实体
 */
export interface EnvConfigRule {
  id: number;
  projectId: number;
  envConfigGroupId: number;
  fromPhase?: StoryPhase;
  toPhase?: StoryPhase;
  tags: string[];
  priority: number;
  createdAt: string;
  envConfigGroup?: EnvConfigGroup;
}

/**
 * 创建环境配置组 DTO
 */
export interface CreateEnvConfigGroupDto {
  name: string;
  description: string;
  variables: CreateEnvVariableDto[];
}

/**
 * 创建环境变量 DTO
 */
export interface CreateEnvVariableDto {
  key: string;
  value: string;
  isRequired: boolean;
  defaultValue?: string;
}

/**
 * 更新环境配置组 DTO
 */
export interface UpdateEnvConfigGroupDto {
  name?: string;
  description?: string;
  variables?: CreateEnvVariableDto[];
}

/**
 * 创建环境变量规则 DTO
 */
export interface CreateEnvConfigRuleDto {
  projectId: number;
  envConfigGroupId: number;
  fromPhase?: StoryPhase;
  toPhase?: StoryPhase;
  tags: string[];
  priority: number;
}

/**
 * 更新环境变量规则 DTO
 */
export interface UpdateEnvConfigRuleDto {
  envConfigGroupId?: number;
  fromPhase?: StoryPhase;
  toPhase?: StoryPhase;
  tags?: string[];
  priority?: number;
}
