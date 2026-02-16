import { createSortable, useDragDropContext, transformStyle } from '@thisbeyond/solid-dnd';
import { Show } from 'solid-js';
import { useKanbanContext } from './kanban-context';
import type { Story } from '../types';
import { pauseStory, startStory } from '../api/client';

const PRIORITY_COLORS = {
  High: '#ef4444',
  Medium: '#f59e0b',
  Low: '#22c55e',
};

const PHASE_LABELS: Record<string, string> = {
  Pending: '待处理',
  RequirementsPlanning: '需求规划',
  AcceptancePlanning: '验收规划',
  Executing: '执行中',
  Testing: '测试中',
  Acceptance: '验收中',
  Completed: '已完成',
  Failed: '失败',
};

interface TaskCardProps {
  story: Story;
}

export function TaskCard(props: TaskCardProps) {
  const kanban = useKanbanContext();
  const sortable = createSortable(props.story.id);

  const handleCardClick = (e: MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    console.log('Task card clicked:', props.story.id);
    kanban.openStoryDetail(props.story);
  };

  const handlePlayPause = async (e: MouseEvent) => {
    console.log('Play button clicked, runningStatus:', props.story.runningStatus);
    e.stopPropagation();
    e.preventDefault();
    try {
      if (props.story.runningStatus === 'Paused') {
        await startStory(props.story.id);
      } else {
        await pauseStory(props.story.id);
      }
      kanban.refreshStories();
    } catch (err) {
      console.error('Failed to toggle story:', err);
    }
  };

  return (
    <div
      ref={sortable.ref}
      classList={{
        'happy-kanban-task': true,
        'happy-kanban-task--dragging': sortable.isActiveDraggable,
        'happy-kanban-task--paused': props.story.runningStatus === 'Paused',
        'happy-kanban-task--running': props.story.runningStatus === 'Running',
        'happy-kanban-task--pending': props.story.runningStatus === 'Pending',
      }}
      style={transformStyle(sortable.transform)}
      onClick={handleCardClick}
      onMouseDown={(e) => {
        // Allow click but prevent drag on the play button area
        if ((e.target as HTMLElement).closest('.happy-kanban-task__actions')) {
          e.stopPropagation();
        }
      }}
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
            <Show when={props.story.runningStatus !== 'Paused'}>
              <span
                class="happy-kanban-task__status-badge"
                classList={{
                  'happy-kanban-task__status-badge--running': props.story.runningStatus === 'Running',
                  'happy-kanban-task__status-badge--pending': props.story.runningStatus === 'Pending',
                }}
              >
                {props.story.runningStatus === 'Running' ? '运行中' : '排队中'}
              </span>
            </Show>
            <Show when={props.story.round > 0}>
              <span class="happy-kanban-task__round-badge">R{props.story.round}</span>
            </Show>
          </div>
          <div class="happy-kanban-task__actions">
            <button
              class={`happy-kanban-task__play-btn ${props.story.runningStatus === 'Paused' ? '' : 'running'}`}
              onClick={handlePlayPause}
              title={props.story.runningStatus === 'Paused' ? '启动' : '暂停'}
            >
              {props.story.runningStatus === 'Paused' ? '▶' : '⏸'}
            </button>
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
