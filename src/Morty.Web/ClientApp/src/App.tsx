/**
 * Morty 应用主组件
 * 应用程序入口点，包含项目选择页面和 Kanban 面板
 */

import { createSignal, Show, createResource } from 'solid-js';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import { ProjectList } from './project';
import { EnvConfigGroupList, EnvConfigRulesList } from './env-config';
import type { Project } from './types';
import { fetchProjects } from './api/client';

type ViewType = 'projects' | 'env-config' | 'kanban';

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
  const [activeTab, setActiveTab] = createSignal<'kanban' | 'settings'>('kanban');

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
        <div class="morty-header__tabs">
          <button
            class={`morty-header__tab ${activeTab() === 'kanban' ? 'morty-header__tab--active' : ''}`}
            onClick={() => setActiveTab('kanban')}
          >
            看板
          </button>
          <button
            class={`morty-header__tab ${activeTab() === 'settings' ? 'morty-header__tab--active' : ''}`}
            onClick={() => setActiveTab('settings')}
          >
            设置
          </button>
        </div>
        <div class="morty-header__spacer" />
        <ConnectionBadge />
      </header>
      <main class="morty-main">
        <Show when={activeTab() === 'kanban'}>
          <KanbanBoard />
        </Show>
        <Show when={activeTab() === 'settings'}>
          <ProjectSettings projectId={props.project.id} />
        </Show>
      </main>
    </>
  );
}

/**
 * 项目设置视图
 */
function ProjectSettings(props: { projectId: number }) {
  return (
    <div class="morty-project-settings">
      <h2 class="morty-project-settings__title">项目设置</h2>
      <div class="morty-project-settings__section">
        <EnvConfigRulesList projectId={props.projectId} />
      </div>
    </div>
  );
}

/**
 * 导航栏组件
 */
function Navigation(props: { currentView: ViewType; onNavigate: (view: ViewType) => void }) {
  return (
    <nav class="morty-nav">
      <button
        class={`morty-nav__item ${props.currentView === 'projects' ? 'morty-nav__item--active' : ''}`}
        onClick={() => props.onNavigate('projects')}
      >
        📁 项目
      </button>
      <button
        class={`morty-nav__item ${props.currentView === 'env-config' ? 'morty-nav__item--active' : ''}`}
        onClick={() => props.onNavigate('env-config')}
      >
        ⚙️ 环境配置
      </button>
    </nav>
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
  // 当前视图
  const [currentView, setCurrentView] = createSignal<ViewType>('projects');

  // 处理项目选择
  const handleSelectProject = (projectId: number) => {
    const project = projects()?.find((p) => p.id === projectId);
    if (project) {
      setSelectedProject(project);
      setCurrentView('kanban');
    }
  };

  // 返回项目列表
  const handleBackToProjects = () => {
    setSelectedProject(null);
    setCurrentView('projects');
  };

  // 处理导航切换
  const handleNavigate = (view: ViewType) => {
    if (view === 'kanban' && !selectedProject()) {
      // 如果没有选中项目，不能切换到 kanban 视图
      return;
    }
    if (view !== 'kanban') {
      // 切换到其他视图时清除选中的项目
      setSelectedProject(null);
    }
    setCurrentView(view);
  };

  return (
    <Show
      when={currentView() === 'kanban' && selectedProject()}
      fallback={
        <div class="morty-app-shell">
          <header class="morty-header morty-header--centered">
            <h1 class="morty-logo">Morty</h1>
            <Navigation currentView={currentView()} onNavigate={handleNavigate} />
          </header>
          <main class="morty-main morty-main--centered">
            <Show when={currentView() === 'projects'}>
              <ProjectList onSelectProject={handleSelectProject} />
            </Show>
            <Show when={currentView() === 'env-config'}>
              <EnvConfigGroupList />
            </Show>
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
