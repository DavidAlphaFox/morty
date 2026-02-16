/**
 * Morty 应用主组件
 * 应用程序入口点，包含项目选择页面和 Kanban 面板
 */

import { createResource, Show, createMemo } from 'solid-js';
import { Router, Route, useParams, A, useLocation } from '@solidjs/router';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import { ProjectList } from './project';
import { EnvConfigGroupList, EnvConfigRulesList } from './env-config';
import { fetchProjects, fetchProject } from './api/client';

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
 * Kanban 视图（选中项目后显示）
 */
function KanbanView() {
  const params = useParams();
  const kanban = useKanbanContext();
  const location = useLocation();
  const [project] = createResource(() => Number(params.projectId), fetchProject);

  const activeTab = createMemo(() => {
    return location.pathname.includes('/settings') ? 'settings' : 'kanban';
  });

  // 设置项目（进入项目时加入 SignalR 房间）
  kanban.setProject(Number(params.projectId));

  return (
    <Show when={project()} fallback={<div>加载中...</div>}>
      {(project) => (
        <>
          <header class="morty-header">
            <A
              class="morty-header__back-btn"
              href="/"
              title="返回项目列表"
            >
              ←
            </A>
            <h1 class="morty-logo">Morty</h1>
            <div class="morty-header__project-name">{project().name}</div>
            <div class="morty-header__tabs">
              <A
                class={`morty-header__tab ${activeTab() === 'kanban' ? 'morty-header__tab--active' : ''}`}
                href={`/project/${params.projectId}`}
              >
                看板
              </A>
              <A
                class={`morty-header__tab ${activeTab() === 'settings' ? 'morty-header__tab--active' : ''}`}
                href={`/project/${params.projectId}/settings`}
              >
                设置
              </A>
            </div>
            <div class="morty-header__spacer" />
            <ConnectionBadge />
          </header>
          <main class="morty-main">
            <Show when={activeTab() === 'kanban'}>
              <KanbanBoard />
            </Show>
            <Show when={activeTab() === 'settings'}>
              <ProjectSettings projectId={Number(params.projectId)} />
            </Show>
          </main>
        </>
      )}
    </Show>
  );
}

/**
 * 导航栏组件
 */
function Navigation() {
  const location = useLocation();

  const isActive = (path: string) => {
    return location.pathname === path;
  };

  return (
    <nav class="morty-nav">
      <A
        class={`morty-nav__item ${isActive('/') ? 'morty-nav__item--active' : ''}`}
        href="/"
      >
        📁 项目
      </A>
      <A
        class={`morty-nav__item ${isActive('/env-config') ? 'morty-nav__item--active' : ''}`}
        href="/env-config"
      >
        ⚙️ 环境配置
      </A>
    </nav>
  );
}

/**
 * 项目列表页面
 */
function ProjectsPage() {
  const [projects] = createResource(fetchProjects);

  return (
    <div class="morty-app-shell">
      <header class="morty-header morty-header--centered">
        <h1 class="morty-logo">Morty</h1>
        <Navigation />
      </header>
      <main class="morty-main morty-main--centered">
        <ProjectList
          onSelectProject={(projectId) => {
            window.location.href = `/project/${projectId}`;
          }}
        />
      </main>
    </div>
  );
}

/**
 * 环境配置页面
 */
function EnvConfigPage() {
  return (
    <div class="morty-app-shell">
      <header class="morty-header morty-header--centered">
        <h1 class="morty-logo">Morty</h1>
        <Navigation />
      </header>
      <main class="morty-main morty-main--centered">
        <EnvConfigGroupList />
      </main>
    </div>
  );
}

/**
 * 应用根组件
 * 包裹 KanbanProvider 提供全局状态
 */
function App() {
  return (
    <KanbanProvider>
      <Router>
        <Route path="/" component={ProjectsPage} />
        <Route path="/env-config" component={EnvConfigPage} />
        <Route path="/project/:projectId" component={KanbanView} />
        <Route path="/project/:projectId/settings" component={KanbanView} />
      </Router>
    </KanbanProvider>
  );
}

export { App };
