/**
 * 项目列表页面
 * 显示所有项目，支持创建新项目
 */

import { createSignal, createResource, Show, For } from 'solid-js';
import type { Project } from '../types';
import { fetchProjects, createProject, deleteProject } from '../api/client';
import { CreateProjectForm } from './create-project-form';

interface ProjectListProps {
  onSelectProject: (projectId: number) => void;
  onDeleteSuccess?: () => void;
}

export function ProjectList(props: ProjectListProps) {
  const [projects, { refetch }] = createResource(fetchProjects);
  const [showCreateForm, setShowCreateForm] = createSignal(false);

  const handleCreateProject = async (name: string, workingDirectory: string, prdJson: string) => {
    try {
      await createProject({ name, workingDirectory, prdJson });
      setShowCreateForm(false);
      refetch();
    } catch (err) {
      console.error('Failed to create project:', err);
      throw err;
    }
  };

  const handleDeleteProject = async (projectId: number) => {
    try {
      await deleteProject(projectId);
      refetch();
      props.onDeleteSuccess?.();
    } catch (err) {
      console.error('Failed to delete project:', err);
      alert('Failed to delete project');
    }
  };

  return (
    <div class="morty-project-list">
      <div class="morty-project-list__header">
        <h1 class="morty-project-list__title">项目</h1>
        <button
          class="morty-project-list__create-btn"
          onClick={() => setShowCreateForm(true)}
        >
          + 新建项目
        </button>
      </div>

      <Show when={showCreateForm()}>
        <CreateProjectForm
          onSubmit={handleCreateProject}
          onCancel={() => setShowCreateForm(false)}
        />
      </Show>

      <Show
        when={!projects.loading}
        fallback={<div class="morty-project-list__loading">Loading projects...</div>}
      >
        <Show
          when={projects()?.length}
          fallback={
            <div class="morty-project-list__empty">
              <div class="morty-project-list__empty-icon">📁</div>
              <div class="morty-project-list__empty-text">暂无项目</div>
              <div class="morty-project-list__empty-hint">点击上方"新建项目"按钮创建</div>
            </div>
          }
        >
          <div class="morty-project-list__grid">
            <For each={projects()}>
              {(project) => (
                <ProjectCard
                  project={project}
                  onClick={() => props.onSelectProject(project.id)}
                  onDelete={() => handleDeleteProject(project.id)}
                />
              )}
            </For>
          </div>
        </Show>
      </Show>
    </div>
  );
}

function ProjectCard(props: { project: Project; onClick: () => void; onDelete: () => void }) {
  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString();
  };

  const handleDelete = (e: Event) => {
    e.stopPropagation();
    if (confirm(`Are you sure you want to delete project "${props.project.name}"?`)) {
      props.onDelete();
    }
  };

  return (
    <div class="morty-project-card" onClick={props.onClick}>
      <div class="morty-project-card__header">
        <div class="morty-project-card__icon">📁</div>
        <h3 class="morty-project-card__name">{props.project.name}</h3>
        <button
          class="morty-project-card__delete"
          onClick={handleDelete}
          title="Delete project"
        >
          🗑️
        </button>
      </div>
      <div class="morty-project-card__path">{props.project.workingDirectory}</div>
      <div class="morty-project-card__footer">
        <span class="morty-project-card__date">创建时间: {formatDate(props.project.createdAt)}</span>
        <span class="morty-project-card__action">打开 →</span>
      </div>
    </div>
  );
}
