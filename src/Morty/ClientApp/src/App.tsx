/**
 * Morty 应用主组件
 * 应用程序入口点，包含项目选择页面和 Kanban 面板
 */

import { createResource, Show, createMemo, createSignal } from 'solid-js';
import { Router, Route, useParams, A, useLocation } from '@solidjs/router';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import { ProjectList } from './project';
import { EnvConfigGroupList, EnvConfigRulesList } from './env-config';
import { fetchProjects, fetchProject, updateProject } from './api/client';
import { MarkdownEditor, MarkdownViewer } from '@ui/markdown-editor';
import type { Project } from './types';

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
 * 项目 PRD 编辑视图
 */
function ProjectPrd(props: { projectId: number; project: Project }) {
  const [editing, setEditing] = createSignal(false);
  const [draft, setDraft] = createSignal('');
  const [saving, setSaving] = createSignal(false);
  const [content, setContent] = createSignal(props.project.prdJson || '');

  const handleEdit = () => {
    setDraft(content());
    setEditing(true);
  };

  const handleCancel = () => {
    setEditing(false);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await updateProject(props.projectId, { prdJson: draft() });
      setContent(draft());
      setEditing(false);
    } catch (e) {
      console.error('保存 PRD 失败:', e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div class="morty-project-prd">
      <div class="morty-project-prd__header">
        <h2 class="morty-project-prd__title">PRD</h2>
        <Show when={!editing()}>
          <button class="morty-btn morty-btn--secondary" onClick={handleEdit}>
            编辑
          </button>
        </Show>
        <Show when={editing()}>
          <div class="morty-project-prd__actions">
            <button class="morty-btn morty-btn--secondary" onClick={handleCancel} disabled={saving()}>
              取消
            </button>
            <button class="morty-btn morty-btn--primary" onClick={handleSave} disabled={saving()}>
              {saving() ? '保存中...' : '保存'}
            </button>
          </div>
        </Show>
      </div>
      <div class="morty-project-prd__content">
        <Show when={editing()} fallback={
          <Show when={content()} fallback={
            <div class="morty-project-prd__empty">
              尚未编写 PRD，点击"编辑"开始编写产品需求文档。
            </div>
          }>
            <MarkdownViewer content={content()} />
          </Show>
        }>
          <MarkdownEditor
            content={draft()}
            onChange={setDraft}
            placeholder="输入 PRD 内容（支持 Markdown 格式）..."
            minHeight="400px"
          />
        </Show>
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
    if (location.pathname.includes('/prd')) return 'prd';
    if (location.pathname.includes('/settings')) return 'settings';
    return 'kanban';
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
                class={`morty-header__tab ${activeTab() === 'prd' ? 'morty-header__tab--active' : ''}`}
                href={`/project/${params.projectId}/prd`}
              >
                PRD
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
            <Show when={activeTab() === 'prd'}>
              <ProjectPrd projectId={Number(params.projectId)} project={project()} />
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
        <Route path="/project/:projectId/prd" component={KanbanView} />
        <Route path="/project/:projectId/settings" component={KanbanView} />
      </Router>
    </KanbanProvider>
  );
}

export { App };
