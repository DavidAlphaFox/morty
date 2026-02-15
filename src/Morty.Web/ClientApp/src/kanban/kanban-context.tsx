/**
 * Kanban 上下文模块
 * 提供 Kanban 面板所需的状态管理和操作函数
 * 使用 SolidJS 的 context API 实现依赖注入
 */

import { createSignal, createMemo, createEffect, onCleanup, type Accessor, type ParentComponent } from 'solid-js';
import { createSafeContext } from '@ui/shared';
import type { Story, StoryStatus, Priority, KanbanColumn } from '../types';
import { STORY_STATUSES, COLUMN_CONFIG } from '../types';
import { fetchProjectStories, createStory as apiCreateStory, updateStory as apiUpdateStory } from '../api/client';
import { createSignalRConnection, type SignalRCallbacks } from '../api/signalr';

/**
 * Kanban 上下文值接口
 * 定义提供给子组件的所有状态和方法
 */
export interface KanbanContextValue {
  columns: Accessor<KanbanColumn[]>;  // Kanban 列数组
  stories: Accessor<Story[]>;         // 所有故事列表
  selectedStory: Accessor<Story | null>; // 当前选中的故事
  projectId: Accessor<number | null>;   // 当前项目 ID
  connectionStatus: Accessor<'connected' | 'disconnected' | 'connecting'>; // SignalR 连接状态

  setProject: (projectId: number) => void; // 设置当前项目
  addStory: (status: StoryStatus, title: string) => Promise<void>; // 添加新故事
  moveStory: (storyId: number, toStatus: StoryStatus) => Promise<void>; // 移动故事到其他列
  updateStoryPriority: (storyId: number, priority: Priority) => Promise<void>; // 更新故事优先级
  openStoryDetail: (story: Story) => void; // 打开故事详情
  closeStoryDetail: () => void; // 关闭故事详情
  refreshStories: () => Promise<void>; // 刷新故事列表
}

/**
 * 创建 Kanban 安全上下文
 * 确保组件在 KanbanProvider 内部使用
 */
const [KanbanContext, useKanbanContext] = createSafeContext<KanbanContextValue>(
  'Kanban components must be used within KanbanProvider'
);

export { useKanbanContext };

/**
 * Kanban 提供者组件
 * 包装整个 Kanban 面板，提供状态管理和 SignalR 实时通信
 */
export const KanbanProvider: ParentComponent = (props) => {
  // 故事列表状态
  const [stories, setStories] = createSignal<Story[]>([]);
  // 当前选中的故事
  const [selectedStory, setSelectedStory] = createSignal<Story | null>(null);
  // 当前项目 ID
  const [projectId, setProjectId] = createSignal<number | null>(null);
  // SignalR 连接状态
  const [connectionStatus, setConnectionStatus] = createSignal<'connected' | 'disconnected' | 'connecting'>('connecting');

  /**
   * 计算列数据
   * 根据故事状态自动分组
   */
  const columns = createMemo<KanbanColumn[]>(() => {
    const allStories = stories();
    return STORY_STATUSES.map((status) => ({
      id: status,
      title: COLUMN_CONFIG[status].title,
      color: COLUMN_CONFIG[status].color,
      stories: allStories.filter((s) => s.status === status),
    }));
  });

  // SignalR 回调函数
  const signalrCallbacks: SignalRCallbacks = {
    // 当故事更新时的回调
    onStoryUpdated: (story) => {
      setStories((prev) => {
        const idx = prev.findIndex((s) => s.id === story.id);
        if (idx >= 0) {
          // 更新现有故事
          const next = [...prev];
          next[idx] = story;
          return next;
        }
        // 新故事且属于当前项目
        if (story.projectId === projectId()) {
          return [...prev, story];
        }
        return prev;
      });
      // 如果选中的故事被更新，同步更新选中状态
      const sel = selectedStory();
      if (sel && sel.id === story.id) {
        setSelectedStory(story);
      }
    },
    // 当迭代完成时的回调
    onIterationComplete: () => {
      // 重新加载故事以获取最新状态
      const pid = projectId();
      if (pid) {
        fetchProjectStories(pid).then(setStories).catch(console.error);
      }
    },
    // 连接成功
    onConnected: () => setConnectionStatus('connected'),
    // 断开连接
    onDisconnected: () => setConnectionStatus('disconnected'),
    // 重新连接中
    onReconnecting: () => setConnectionStatus('connecting'),
  };

  // 创建 SignalR 连接
  const signalr = createSignalRConnection(signalrCallbacks);

  // 组件卸载时断开连接
  onCleanup(() => {
    signalr.stop();
  });

  /**
   * 刷新故事列表
   */
  const refreshStories = async () => {
    const pid = projectId();
    if (!pid) return;
    try {
      const data = await fetchProjectStories(pid);
      setStories(data);
    } catch (err) {
      console.error('Failed to fetch stories:', err);
    }
  };

  /**
   * 切换当前项目
   * 会先离开旧项目的 SignalR 房间，再加入新项目房间
   */
  const setProject = (id: number) => {
    const prevId = projectId();
    if (prevId) {
      signalr.leaveProject(prevId);
    }
    setProjectId(id);
    setSelectedStory(null);
    signalr.joinProject(id);
    fetchProjectStories(id).then(setStories).catch(console.error);
  };

  /**
   * 添加新故事
   * 使用乐观更新：先显示临时故事，失败则回滚
   */
  const addStory = async (status: StoryStatus, title: string) => {
    const pid = projectId();
    if (!pid) return;

    // 生成故事 ID
    const storyId = `S-${Date.now().toString(36).toUpperCase()}`;

    // 乐观更新：立即添加
    const tempStory: Story = {
      id: -Date.now(),
      projectId: pid,
      storyId,
      title,
      priority: 'Medium',
      status,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    setStories((prev) => [...prev, tempStory]);

    try {
      const created = await apiCreateStory({
        projectId: pid,
        storyId,
        title,
        priority: 'Medium',
      });
      // 用真实故事替换临时故事
      setStories((prev) => prev.map((s) => (s.id === tempStory.id ? created : s)));
    } catch (err) {
      console.error('Failed to create story:', err);
      // 失败时移除临时故事
      setStories((prev) => prev.filter((s) => s.id !== tempStory.id));
    }
  };

  /**
   * 将故事移动到其他列
   * 使用乐观更新，失败则刷新列表
   */
  const moveStory = async (storyId: number, toStatus: StoryStatus) => {
    // 乐观更新
    setStories((prev) =>
      prev.map((s) => (s.id === storyId ? { ...s, status: toStatus } : s))
    );

    try {
      const updated = await apiUpdateStory(storyId, { status: toStatus });
      setStories((prev) => prev.map((s) => (s.id === storyId ? updated : s)));
    } catch (err) {
      console.error('Failed to move story:', err);
      // 失败时刷新列表
      refreshStories();
    }
  };

  /**
   * 更新故事优先级
   */
  const updateStoryPriority = async (storyId: number, priority: Priority) => {
    setStories((prev) =>
      prev.map((s) => (s.id === storyId ? { ...s, priority } : s))
    );

    try {
      const updated = await apiUpdateStory(storyId, { priority });
      setStories((prev) => prev.map((s) => (s.id === storyId ? updated : s)));
      // 更新选中故事
      const sel = selectedStory();
      if (sel && sel.id === storyId) {
        setSelectedStory(updated);
      }
    } catch (err) {
      console.error('Failed to update priority:', err);
      refreshStories();
    }
  };

  /**
   * 打开故事详情面板
   */
  const openStoryDetail = (story: Story) => {
    setSelectedStory(story);
  };

  /**
   * 关闭故事详情面板
   */
  const closeStoryDetail = () => {
    setSelectedStory(null);
  };

  // 构建上下文值
  const context: KanbanContextValue = {
    columns,
    stories,
    selectedStory,
    projectId,
    connectionStatus,
    setProject,
    addStory,
    moveStory,
    updateStoryPriority,
    openStoryDetail,
    closeStoryDetail,
    refreshStories,
  };

  return (
    <KanbanContext.Provider value={context}>
      {props.children}
    </KanbanContext.Provider>
  );
};
