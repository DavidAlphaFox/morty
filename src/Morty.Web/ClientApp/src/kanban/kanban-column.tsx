/**
 * Kanban 列组件
 * 显示特定状态的所有故事卡片
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

  return (
    <div class="happy-kanban-column" ref={droppable.ref}>
      <div class="happy-kanban-column__header">
        <div
          class="happy-kanban-column__indicator"
          style={{ background: props.column.color }}
        />
        <h3 class="happy-kanban-column__title">{props.column.title}</h3>
        <div class="happy-kanban-column__count">{props.column.stories.length}</div>
        <div class="happy-kanban-column__actions">
          <button
            class="happy-kanban-column__add-btn"
            style={{ background: props.column.color }}
            onClick={() => setShowCreateModal(true)}
            title="Add story"
          >
            +
          </button>
        </div>
      </div>

      <div class="happy-kanban-column__content">
        <SortableProvider ids={storyIds()}>
          <For each={props.column.stories}>
            {(story) => <TaskCard story={story} />}
          </For>
        </SortableProvider>
      </div>

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
