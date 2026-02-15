/**
 * Morty 应用主组件
 * 应用程序入口点，包含项目选择器和 Kanban 面板
 */

import { createSignal, createResource, Show, For, onCleanup } from 'solid-js';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import type { Project } from './types';
import { fetchProjects } from './api/client';

/**
 * 项目选择器组件
 * 显示项目下拉列表，允许用户切换当前项目
 */
function ProjectSelector(props: {
  projects: Project[];
  selected: number | null;
  onSelect: (id: number) => void;
}) {
  // 下拉框展开状态
  const [open, setOpen] = createSignal(false);

  // 获取当前选中的项目
  const selectedProject = () => props.projects.find((p) => p.id === props.selected);

  // 点击外部关闭下拉框
  const handleClickOutside = (e: MouseEvent) => {
    if (!(e.target as HTMLElement).closest('.morty-project-selector')) {
      setOpen(false);
    }
  };

  // 组件挂载时添加点击事件监听
  if (typeof document !== 'undefined') {
    document.addEventListener('click', handleClickOutside);
    // 组件卸载时移除事件监听
    onCleanup(() => document.removeEventListener('click', handleClickOutside));
  }

  return (
    <div class="morty-project-selector">
      <button
        class="morty-project-selector__btn"
        onClick={() => setOpen(!open())}
      >
        {selectedProject()?.name || 'Select project'}
        <span style={{ 'margin-left': 'auto', opacity: '0.5' }}>&#9662;</span>
      </button>
      <Show when={open()}>
        <div class="morty-project-selector__dropdown">
          <For each={props.projects}>
            {(project) => (
              <button
                classList={{
                  'morty-project-selector__item': true,
                  'morty-project-selector__item--active': project.id === props.selected,
                }}
                onClick={() => {
                  props.onSelect(project.id);
                  setOpen(false);
                }}
              >
                {project.name}
              </button>
            )}
          </For>
        </div>
      </Show>
    </div>
  );
}

/**
 * SignalR 连接状态指示器
 * 显示当前的实时连接状态
 */
function ConnectionBadge() {
  const kanban = useKanbanContext();

  // 根据状态返回对应的 CSS 类名
  const statusClass = () => {
    switch (kanban.connectionStatus()) {
      case 'connected': return 'morty-connection--connected';
      case 'disconnected': return 'morty-connection--disconnected';
      case 'connecting': return 'morty-connection--connecting';
    }
  };

  // 根据状态返回显示文本
  const statusText = () => {
    switch (kanban.connectionStatus()) {
      case 'connected': return 'Connected';
      case 'disconnected': return 'Disconnected';
      case 'connecting': return 'Connecting...';
    }
  };

  return (
    <div class={`morty-connection ${statusClass()}`}>
      <span class="morty-connection__dot" />
      {statusText()}
    </div>
  );
}

/**
 * 应用主内容区
 * 包含顶部导航栏和 Kanban 面板
 */
function AppContent() {
  const kanban = useKanbanContext();
  // 加载项目列表
  const [projects] = createResource(fetchProjects);

  // 处理项目选择
  const handleProjectSelect = (id: number) => {
    kanban.setProject(id);
  };

  return (
    <>
      <header class="morty-header">
        <h1 class="morty-logo">Morty</h1>
        <div class="morty-header__spacer" />
        <Show when={projects()}>
          {(projs) => (
            <ProjectSelector
              projects={projs()}
              selected={kanban.projectId()}
              onSelect={handleProjectSelect}
            />
          )}
        </Show>
        <ConnectionBadge />
      </header>
      <main class="morty-main">
        <Show
          when={kanban.projectId()}
          fallback={
            <div class="morty-empty">
              <div class="morty-empty__icon">&#9776;</div>
              <div class="morty-empty__text">Select a project to get started</div>
            </div>
          }
        >
          <KanbanBoard />
        </Show>
      </main>
    </>
  );
}

/**
 * 应用根组件
 * 包裹 KanbanProvider 提供全局状态
 */
export function App() {
  return (
    <KanbanProvider>
      <AppContent />
    </KanbanProvider>
  );
}
