/**
 * Kanban 列组件
 * 显示特定状态的所有故事卡片，支持折叠/展开
 */

import { createSignal, For, Show } from 'solid-js';
import { createDroppable, SortableProvider } from '@thisbeyond/solid-dnd';
import { useKanbanContext } from './kanban-context';
import { TaskCard } from './task-card';
import { CreateStoryModal, type CreateStoryData } from './create-story-modal';
import type { KanbanColumn as KanbanColumnType } from '../types';

interface KanbanColumnProps {
  column: KanbanColumnType;
}

export function KanbanColumn(props: KanbanColumnProps) {
  const kanban = useKanbanContext();
  const droppable = createDroppable(props.column.id);
  const [showCreateModal, setShowCreateModal] = createSignal(false);
  const [collapsed, setCollapsed] = createSignal(false);

  const storyIds = () => props.column.stories.map((s) => s.id);

  const handleCreateStory = async (data: CreateStoryData) => {
    await kanban.addStory(
      props.column.id,
      data.title,
      data.priority,
      data.requirements,
      data.userAcceptanceCriteria,
      data.dependencies
    );
    setShowCreateModal(false);
  };

  const toggleCollapse = () => setCollapsed(!collapsed());

  return (
    <div
      classList={{
        'happy-kanban-column': true,
        'happy-kanban-column--collapsed': collapsed(),
      }}
      ref={droppable.ref}
    >
      <div class="happy-kanban-column__header" onClick={toggleCollapse}>
        <div
          class="happy-kanban-column__indicator"
          style={{ background: props.column.color }}
        />
        <h3 class="happy-kanban-column__title">{props.column.title}</h3>
        <div class="happy-kanban-column__count">{props.column.stories.length}</div>
        <span class="happy-kanban-column__chevron">{collapsed() ? '▸' : '▾'}</span>
        <Show when={props.column.id === 'Pending'}>
          <div class="happy-kanban-column__actions">
            <button
              class="happy-kanban-column__add-btn"
              style={{ background: props.column.color }}
              onClick={(e) => { e.stopPropagation(); setShowCreateModal(true); }}
              title="添加任务"
            >
              +
            </button>
          </div>
        </Show>
      </div>

      <Show when={!collapsed()}>
        <div class="happy-kanban-column__content">
          <SortableProvider ids={storyIds()}>
            <For each={props.column.stories}>
              {(story) => <TaskCard story={story} />}
            </For>
          </SortableProvider>
        </div>
      </Show>

      <Show when={showCreateModal()}>
        <CreateStoryModal
          columnId={props.column.id}
          onSubmit={handleCreateStory}
          onCancel={() => setShowCreateModal(false)}
        />
      </Show>
    </div>
  );
}
