import { createSignal, For, Show } from 'solid-js';
import { createDroppable, SortableProvider } from '@thisbeyond/solid-dnd';
import { useKanbanContext } from './kanban-context';
import { TaskCard } from './task-card';
import { Input } from '@ui/input';
import { Button } from '@ui/button';
import type { KanbanColumn as KanbanColumnType, StoryStatus } from '../types';

interface KanbanColumnProps {
  column: KanbanColumnType;
}

export function KanbanColumn(props: KanbanColumnProps) {
  const kanban = useKanbanContext();
  const droppable = createDroppable(props.column.id);
  const [showAddForm, setShowAddForm] = createSignal(false);
  const [newTitle, setNewTitle] = createSignal('');

  const storyIds = () => props.column.stories.map((s) => s.id);

  const handleAdd = async () => {
    const title = newTitle().trim();
    if (!title) return;
    await kanban.addStory(props.column.id as StoryStatus, title);
    setNewTitle('');
    setShowAddForm(false);
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAdd();
    } else if (e.key === 'Escape') {
      setShowAddForm(false);
      setNewTitle('');
    }
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
            onClick={() => setShowAddForm(true)}
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

        <Show when={showAddForm()}>
          <div class="happy-kanban-column__add-form">
            <Input
              placeholder="Story title..."
              value={newTitle()}
              onInput={(e) => setNewTitle(e.currentTarget.value)}
              onKeyDown={handleKeyDown}
              autofocus
            />
            <div class="happy-kanban-column__add-actions">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setShowAddForm(false);
                  setNewTitle('');
                }}
              >
                Cancel
              </Button>
              <Button variant="primary" size="sm" onClick={handleAdd}>
                Add
              </Button>
            </div>
          </div>
        </Show>
      </div>
    </div>
  );
}
