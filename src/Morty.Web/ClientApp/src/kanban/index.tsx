import { createSignal, For, Show } from 'solid-js';
import {
  DragDropProvider,
  DragDropSensors,
  DragOverlay,
  closestCenter,
} from '@thisbeyond/solid-dnd';
import { useKanbanContext } from './kanban-context';
import { KanbanColumn } from './kanban-column';
import { TaskCardOverlay } from './task-card';
import { TaskDetailSheet } from './task-detail-sheet';
import type { Story, StoryStatus } from '../types';
import { STORY_STATUSES } from '../types';

export function KanbanBoard() {
  const kanban = useKanbanContext();
  const [activeStory, setActiveStory] = createSignal<Story | null>(null);

  const findStoryById = (id: number): Story | undefined => {
    return kanban.stories().find((s) => s.id === id);
  };

  const findColumnForStory = (storyId: number): StoryStatus | undefined => {
    const story = findStoryById(storyId);
    return story?.status as StoryStatus | undefined;
  };

  const handleDragStart = ({ draggable }: any) => {
    const story = findStoryById(draggable.id);
    if (story) {
      setActiveStory(story);
    }
  };

  const handleDragEnd = ({ draggable, droppable }: any) => {
    setActiveStory(null);

    if (!draggable || !droppable) return;

    const storyId = draggable.id as number;
    const targetColumnId = droppable.id as string;

    // Check if target is a valid column status
    if (STORY_STATUSES.includes(targetColumnId as StoryStatus)) {
      const currentStatus = findColumnForStory(storyId);
      if (currentStatus && currentStatus !== targetColumnId) {
        kanban.moveStory(storyId, targetColumnId as StoryStatus);
      }
    }
  };

  return (
    <div class="happy-kanban">
      <DragDropProvider
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
        collisionDetector={closestCenter}
      >
        <DragDropSensors />
        <div class="happy-kanban__board">
          <For each={kanban.columns()}>
            {(column) => <KanbanColumn column={column} />}
          </For>
        </div>
        <DragOverlay>
          <Show when={activeStory()}>
            {(story) => <TaskCardOverlay story={story()} />}
          </Show>
        </DragOverlay>
      </DragDropProvider>
      <TaskDetailSheet />
    </div>
  );
}
