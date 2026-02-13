import { createSignal, createResource, Show, For } from 'solid-js';
import { SheetRoot, SheetContent } from '@ui/sheet';
import { TabsRoot, TabsList, TabsTrigger, TabsContent } from '@ui/tabs';
import { Input } from '@ui/input';
import { useKanbanContext } from './kanban-context';
import { STORY_STATUSES, COLUMN_CONFIG } from '../types';
import type { Story, StoryStatus, Priority, Iteration, Plan } from '../types';
import { fetchStoryIterations, fetchStoryPlan } from '../api/client';

const PRIORITY_COLORS: Record<Priority, string> = {
  High: '#ef4444',
  Medium: '#f59e0b',
  Low: '#22c55e',
};

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleString();
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  return `${(ms / 60000).toFixed(1)}m`;
}

function TaskDetailContent(props: { story: Story }) {
  const kanban = useKanbanContext();
  const [showStatusMenu, setShowStatusMenu] = createSignal(false);

  const [iterations] = createResource(
    () => props.story.id,
    (id) => fetchStoryIterations(id)
  );

  const [plan] = createResource(
    () => props.story.id,
    (id) => fetchStoryPlan(id)
  );

  const statusConfig = () => COLUMN_CONFIG[props.story.status as StoryStatus];

  const handleStatusChange = (status: StoryStatus) => {
    kanban.moveStory(props.story.id, status);
    setShowStatusMenu(false);
  };

  const handlePriorityChange = (priority: Priority) => {
    kanban.updateStoryPriority(props.story.id, priority);
  };

  return (
    <div class="happy-kanban-detail">
      {/* Toolbar */}
      <div class="happy-kanban-detail__toolbar">
        <div class="happy-kanban-detail__status-dropdown">
          <button
            class="happy-kanban-detail__status-btn"
            style={{ '--status-color': statusConfig().color }}
            onClick={() => setShowStatusMenu(!showStatusMenu())}
          >
            <span
              class="happy-kanban-detail__status-dot"
              style={{ background: statusConfig().color }}
            />
            {statusConfig().title}
          </button>
          <Show when={showStatusMenu()}>
            <div class="happy-kanban-detail__status-menu">
              <For each={STORY_STATUSES}>
                {(status) => (
                  <button
                    class="happy-kanban-detail__status-menu-item"
                    onClick={() => handleStatusChange(status)}
                  >
                    <span
                      class="happy-kanban-detail__status-dot"
                      style={{ background: COLUMN_CONFIG[status].color }}
                    />
                    {COLUMN_CONFIG[status].title}
                  </button>
                )}
              </For>
            </div>
          </Show>
        </div>

        <div class="happy-kanban-detail__toolbar-spacer" />

        <button
          class="happy-kanban-detail__icon-btn"
          onClick={() => kanban.closeStoryDetail()}
          title="Close"
        >
          ✕
        </button>
      </div>

      {/* Tabs */}
      <TabsRoot defaultValue="overview" class="happy-kanban-detail__tabs">
        <TabsList class="happy-kanban-detail__tabs-list">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="iterations">Iterations</TabsTrigger>
          <TabsTrigger value="plan">Plan</TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" class="happy-kanban-detail__panel">
          <div class="happy-kanban-detail__overview">
            {/* Story ID */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Story ID</div>
              <div class="happy-kanban-detail__row-content">
                <code style={{ color: 'var(--color-primary)', "font-weight": '600' }}>
                  {props.story.storyId}
                </code>
              </div>
            </div>

            {/* Title */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Title</div>
              <div class="happy-kanban-detail__row-content">
                <input
                  class="happy-kanban-detail__name-input"
                  value={props.story.title}
                  readOnly
                />
              </div>
            </div>

            {/* Priority */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Priority</div>
              <div class="happy-kanban-detail__row-content">
                <div class="happy-kanban-detail__priority-group">
                  <For each={(['High', 'Medium', 'Low'] as Priority[])}>
                    {(p) => (
                      <button
                        classList={{
                          'happy-kanban-detail__priority-btn': true,
                          'happy-kanban-detail__priority-btn--active': props.story.priority === p,
                        }}
                        onClick={() => handlePriorityChange(p)}
                      >
                        <span style={{
                          display: 'inline-block',
                          width: '8px',
                          height: '8px',
                          'border-radius': '50%',
                          background: PRIORITY_COLORS[p],
                        }} />
                        {p}
                      </button>
                    )}
                  </For>
                </div>
              </div>
            </div>

            {/* Status */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Status</div>
              <div class="happy-kanban-detail__row-content">
                <span style={{
                  color: statusConfig().color,
                  'font-weight': '600',
                  'font-size': '0.875rem',
                }}>
                  {statusConfig().title}
                </span>
              </div>
            </div>

            {/* Created */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Created</div>
              <div class="happy-kanban-detail__row-content" style={{ 'font-size': '0.875rem', color: 'var(--color-muted-foreground)' }}>
                {formatDate(props.story.createdAt)}
              </div>
            </div>

            {/* Completed */}
            <Show when={props.story.completedAt}>
              <div class="happy-kanban-detail__row">
                <div class="happy-kanban-detail__row-label">Completed</div>
                <div class="happy-kanban-detail__row-content" style={{ 'font-size': '0.875rem', color: 'var(--color-muted-foreground)' }}>
                  {formatDate(props.story.completedAt)}
                </div>
              </div>
            </Show>
          </div>
        </TabsContent>

        {/* Iterations Tab */}
        <TabsContent value="iterations" class="happy-kanban-detail__panel">
          <Show
            when={!iterations.loading}
            fallback={<div class="happy-kanban-detail__empty"><p>Loading iterations...</p></div>}
          >
            <Show
              when={iterations()?.length}
              fallback={<div class="happy-kanban-detail__empty"><p>No iterations yet</p></div>}
            >
              <div class="happy-kanban-detail__iterations">
                <For each={iterations()}>
                  {(iter) => (
                    <div class="happy-kanban-detail__iteration">
                      <div class="happy-kanban-detail__iteration-header">
                        <span class="happy-kanban-detail__iteration-num">
                          Iteration #{iter.iterationNum}
                        </span>
                        <span class="happy-kanban-detail__iteration-duration">
                          {formatDuration(iter.durationMs)}
                        </span>
                      </div>
                      <div class="happy-kanban-detail__iteration-time">
                        {formatDate(iter.startedAt)}
                        {iter.completedAt ? ` → ${formatDate(iter.completedAt)}` : ' (running)'}
                      </div>
                      <Show when={iter.output}>
                        <div class="happy-kanban-detail__iteration-output">
                          {iter.output}
                        </div>
                      </Show>
                    </div>
                  )}
                </For>
              </div>
            </Show>
          </Show>
        </TabsContent>

        {/* Plan Tab */}
        <TabsContent value="plan" class="happy-kanban-detail__panel">
          <Show
            when={!plan.loading}
            fallback={<div class="happy-kanban-detail__empty"><p>Loading plan...</p></div>}
          >
            <Show
              when={plan()}
              fallback={<div class="happy-kanban-detail__empty"><p>No plan generated yet</p></div>}
            >
              {(planData) => (
                <div class="happy-kanban-detail__plan">
                  <div class="happy-kanban-detail__plan-content">
                    {planData().planContent}
                  </div>
                  <div style={{ 'font-size': '0.75rem', color: 'var(--color-muted-foreground)' }}>
                    Generated: {formatDate(planData().createdAt)}
                  </div>
                </div>
              )}
            </Show>
          </Show>
        </TabsContent>
      </TabsRoot>
    </div>
  );
}

export function TaskDetailSheet() {
  const kanban = useKanbanContext();

  return (
    <SheetRoot
      open={!!kanban.selectedStory()}
      onOpenChange={(open) => {
        if (!open) kanban.closeStoryDetail();
      }}
    >
      <Show when={kanban.selectedStory()}>
        {(story) => (
          <SheetContent side="right">
            <TaskDetailContent story={story()} />
          </SheetContent>
        )}
      </Show>
    </SheetRoot>
  );
}
