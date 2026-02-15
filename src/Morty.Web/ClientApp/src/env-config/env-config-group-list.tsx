/**
 * 环境配置组列表组件
 * 显示所有全局环境配置组
 */

import { createSignal, createResource, Show, For } from 'solid-js';
import type { EnvConfigGroup } from '../types';
import { fetchEnvConfigGroups, deleteEnvConfigGroup } from '../api/client';
import { CreateEnvConfigGroupForm } from './create-env-config-group-form';
import { Button } from '@ui/button';

export function EnvConfigGroupList() {
  const [groups, { refetch }] = createResource(fetchEnvConfigGroups);
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [editingGroup, setEditingGroup] = createSignal<EnvConfigGroup | null>(null);

  const handleDelete = async (id: number) => {
    if (!confirm('确定要删除这个环境配置组吗？')) return;

    try {
      await deleteEnvConfigGroup(id);
      refetch();
    } catch (err) {
      console.error('Failed to delete env config group:', err);
      alert('删除失败');
    }
  };

  const handleCreateSuccess = () => {
    setShowCreateForm(false);
    setEditingGroup(null);
    refetch();
  };

  const handleEdit = (group: EnvConfigGroup) => {
    setEditingGroup(group);
    setShowCreateForm(true);
  };

  return (
    <div class="morty-env-config-group-list">
      <div class="morty-env-config-group-list__header">
        <h2 class="morty-env-config-group-list__title">环境配置组</h2>
        <button
          class="morty-env-config-group-list__create-btn"
          onClick={() => setShowCreateForm(true)}
        >
          + 新建配置组
        </button>
      </div>

      <Show when={showCreateForm()}>
        <CreateEnvConfigGroupForm
          editingGroup={editingGroup()}
          onSuccess={handleCreateSuccess}
          onCancel={() => {
            setShowCreateForm(false);
            setEditingGroup(null);
          }}
        />
      </Show>

      <Show
        when={!groups.loading}
        fallback={<div class="morty-env-config-group-list__loading">加载中...</div>}
      >
        <Show
          when={groups()?.length}
          fallback={
            <div class="morty-env-config-group-list__empty">
              <div class="morty-env-config-group-list__empty-icon">⚙️</div>
              <div class="morty-env-config-group-list__empty-text">暂无配置组</div>
              <div class="morty-env-config-group-list__empty-hint">创建环境配置组来管理环境变量</div>
            </div>
          }
        >
          <div class="morty-env-config-group-list__grid">
            <For each={groups()}>
              {(group) => (
                <EnvConfigGroupCard
                  group={group}
                  onEdit={() => handleEdit(group)}
                  onDelete={() => handleDelete(group.id)}
                />
              )}
            </For>
          </div>
        </Show>
      </Show>
    </div>
  );
}

function EnvConfigGroupCard(props: {
  group: EnvConfigGroup;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString();
  };

  return (
    <div class="morty-env-config-group-card">
      <div class="morty-env-config-group-card__header">
        <div class="morty-env-config-group-card__icon">⚙️</div>
        <h3 class="morty-env-config-group-card__name">{props.group.name}</h3>
      </div>

      <div class="morty-env-config-group-card__description">
        {props.group.description || '无描述'}
      </div>

      <div class="morty-env-config-group-card__variables">
        <div class="morty-env-config-group-card__variables-title">
          环境变量 ({props.group.variables.length})
        </div>
        <Show when={props.group.variables.length > 0}>
          <div class="morty-env-config-group-card__variables-list">
            <For each={props.group.variables.slice(0, 3)}>
              {(variable) => (
                <div class="morty-env-config-group-card__variable">
                  <span class="morty-env-config-group-card__variable-key">
                    {variable.key}
                  </span>
                  {variable.isRequired && (
                    <span class="morty-env-config-group-card__variable-required">*</span>
                  )}
                </div>
              )}
            </For>
            <Show when={props.group.variables.length > 3}>
              <div class="morty-env-config-group-card__variable-more">
                +{props.group.variables.length - 3} more
              </div>
            </Show>
          </div>
        </Show>
      </div>

      <div class="morty-env-config-group-card__footer">
        <span class="morty-env-config-group-card__date">
          创建于: {formatDate(props.group.createdAt)}
        </span>
        <div class="morty-env-config-group-card__actions">
          <button
            class="morty-env-config-group-card__action"
            onClick={props.onEdit}
          >
            编辑
          </button>
          <button
            class="morty-env-config-group-card__action morty-env-config-group-card__action--danger"
            onClick={props.onDelete}
          >
            删除
          </button>
        </div>
      </div>
    </div>
  );
}
