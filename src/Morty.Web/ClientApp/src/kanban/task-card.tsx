import { createSortable, useDragDropContext, transformStyle } from '@thisbeyond/solid-dnd';
import { useKanbanContext } from './kanban-context';
import type { Story } from '../types';

const PRIORITY_COLORS = {
  High: '#ef4444',
  Medium: '#f59e0b',
  Low: '#22c55e',
};

interface TaskCardProps {
  story: Story;
}

export function TaskCard(props: TaskCardProps) {
  const kanban = useKanbanContext();
  const sortable = createSortable(props.story.id);
  const [dndState] = useDragDropContext()!;

  return (
    <div
      ref={sortable.ref}
      classList={{
        'happy-kanban-task': true,
        'happy-kanban-task--dragging': sortable.isActiveDraggable,
      }}
      style={transformStyle(sortable.transform)}
      {...sortable.dragActivators}
      onClick={() => kanban.openStoryDetail(props.story)}
    >
      <div class="happy-kanban-task__body">
        <div
          class="happy-kanban-task__priority-dot"
          style={{ background: PRIORITY_COLORS[props.story.priority] }}
        />
        <div class="happy-kanban-task__story-id">{props.story.storyId}</div>
        <h4 class="happy-kanban-task__title">{props.story.title}</h4>
        <div class="happy-kanban-task__footer">
          <div class="happy-kanban-task__meta">
            <span class="happy-kanban-task__stat">{props.story.priority}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

export function TaskCardOverlay(props: { story: Story }) {
  return (
    <div class="happy-kanban-task happy-kanban-task--overlay">
      <div class="happy-kanban-task__body">
        <div
          class="happy-kanban-task__priority-dot"
          style={{ background: PRIORITY_COLORS[props.story.priority] }}
        />
        <div class="happy-kanban-task__story-id">{props.story.storyId}</div>
        <h4 class="happy-kanban-task__title">{props.story.title}</h4>
      </div>
    </div>
  );
}
