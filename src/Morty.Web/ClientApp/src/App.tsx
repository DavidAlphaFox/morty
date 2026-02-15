/**
 * Morty 应用主组件
 * 应用程序入口点，包含项目选择页面和 Kanban 面板
 */

import { createSignal, Show, createResource } from 'solid-js';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import { ProjectList } from './project';
import type { Project } from './types';
import { fetchProjects } from './api/client';

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
 * Kanban 视图（选中项目后显示）
 */
function KanbanView(props: { project: Project; onBack: () => void }) {
  const kanban = useKanbanContext();

  // 设置项目（进入项目时加入 SignalR 房间）
  kanban.setProject(props.project.id);

  return (
    <>
      <header class="morty-header">
        <button
          class="morty-header__back-btn"
          onClick={props.onBack}
          title="Back to projects"
        >
          ←
        </button>
        <h1 class="morty-logo">Morty</h1>
        <div class="morty-header__project-name">{props.project.name}</div>
        <div class="morty-header__spacer" />
        <ConnectionBadge />
      </header>
      <main class="morty-main">
        <KanbanBoard />
      </main>
    </>
  );
}

/**
 * 应用主内容区
 * 根据是否选中项目显示不同视图
 */
function AppContent() {
  const kanban = useKanbanContext();
  // 加载项目列表
  const [projects] = createResource(fetchProjects);
  // 当前选中的项目
  const [selectedProject, setSelectedProject] = createSignal<Project | null>(null);

  // 处理项目选择
  const handleSelectProject = (projectId: number) => {
    const project = projects()?.find((p) => p.id === projectId);
    if (project) {
      setSelectedProject(project);
    }
  };

  // 返回项目列表
  const handleBackToProjects = () => {
    setSelectedProject(null);
  };

  return (
    <Show
      when={selectedProject()}
      fallback={
        <div class="morty-app-shell">
          <header class="morty-header morty-header--centered">
            <h1 class="morty-logo">Morty</h1>
          </header>
          <main class="morty-main morty-main--centered">
            <ProjectList onSelectProject={handleSelectProject} />
          </main>
        </div>
      }
    >
      {(project) => <KanbanView project={project()} onBack={handleBackToProjects} />}
    </Show>
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
