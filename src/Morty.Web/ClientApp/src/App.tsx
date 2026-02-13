import { createSignal, createResource, Show, For, onCleanup } from 'solid-js';
import { KanbanProvider, useKanbanContext } from './kanban/kanban-context';
import { KanbanBoard } from './kanban';
import type { Project } from './types';
import { fetchProjects } from './api/client';

function ProjectSelector(props: {
  projects: Project[];
  selected: number | null;
  onSelect: (id: number) => void;
}) {
  const [open, setOpen] = createSignal(false);

  const selectedProject = () => props.projects.find((p) => p.id === props.selected);

  const handleClickOutside = (e: MouseEvent) => {
    if (!(e.target as HTMLElement).closest('.morty-project-selector')) {
      setOpen(false);
    }
  };

  if (typeof document !== 'undefined') {
    document.addEventListener('click', handleClickOutside);
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

function ConnectionBadge() {
  const kanban = useKanbanContext();

  const statusClass = () => {
    switch (kanban.connectionStatus()) {
      case 'connected': return 'morty-connection--connected';
      case 'disconnected': return 'morty-connection--disconnected';
      case 'connecting': return 'morty-connection--connecting';
    }
  };

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

function AppContent() {
  const kanban = useKanbanContext();
  const [projects] = createResource(fetchProjects);

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

export function App() {
  return (
    <KanbanProvider>
      <AppContent />
    </KanbanProvider>
  );
}
