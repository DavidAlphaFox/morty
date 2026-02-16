/**
 * 任务详情侧边栏
 * 显示故事的详细信息，包括需求和验收标准
 * 使用 Markdown 编辑器进行内容编辑
 */

import { createSignal, createResource, createEffect, Show, For } from 'solid-js';
import { Portal } from 'solid-js/web';
import { SheetRoot, SheetContent } from '@ui/sheet';
import { TabsRoot, TabsList, TabsTrigger, TabsContent } from '@ui/tabs';
import { Button } from '@ui/button';
import { MarkdownEditor, MarkdownViewer } from '@ui/markdown-editor';
import { useKanbanContext } from './kanban-context';
import {
  KANBAN_COLUMNS,
  KANBAN_COLUMN_CONFIG,
  PHASE_TO_COLUMN,
  PHASE_CONFIG,
} from '../types';
import type { Story, KanbanColumnId, Priority, Iteration, RoundSummary } from '../types';
import { fetchStoryIterations, fetchStoryRounds, fetchStoryIterationsByRound, regeneratePlan, regenerateAcceptance } from '../api/client';

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

/** 全屏内容预览模态框 */
function ContentModal(props: {
  open: boolean;
  title: string;
  content: string;
  isMarkdown?: boolean;
  onClose: () => void;
}) {
  return (
    <Show when={props.open}>
      <Portal>
        <div class="content-modal">
          <div class="content-modal__backdrop" />
          <div class="content-modal__dialog">
            <div class="content-modal__header">
              <h3 class="content-modal__title">{props.title}</h3>
              <button class="content-modal__close" onClick={() => props.onClose()}>✕</button>
            </div>
            <div class="content-modal__body">
              <Show when={props.isMarkdown} fallback={
                <pre class="content-modal__pre">{props.content}</pre>
              }>
                <MarkdownViewer content={props.content} />
              </Show>
            </div>
          </div>
        </div>
      </Portal>
    </Show>
  );
}

/** 全屏编辑模态框 */
function EditorModal(props: {
  open: boolean;
  title: string;
  content: string;
  onChange: (val: string) => void;
  onSave: () => void;
  onClose: () => void;
  placeholder?: string;
}) {
  return (
    <Show when={props.open}>
      <Portal>
        <div class="content-modal">
          <div class="content-modal__backdrop" />
          <div class="content-modal__dialog content-modal__dialog--editor">
            <div class="content-modal__header">
              <h3 class="content-modal__title">{props.title}</h3>
              <button class="content-modal__close" onClick={() => props.onClose()}>✕</button>
            </div>
            <div class="content-modal__body content-modal__body--editor">
              <MarkdownEditor
                content={props.content}
                onChange={props.onChange}
                placeholder={props.placeholder || '输入内容（支持 Markdown 格式）...'}
                minHeight="400px"
              />
            </div>
            <div class="content-modal__footer">
              <Button variant="ghost" onClick={props.onClose}>取消</Button>
              <Button variant="primary" onClick={props.onSave}>保存</Button>
            </div>
          </div>
        </div>
      </Portal>
    </Show>
  );
}

function IterationItem(props: { iter: Iteration; onShowOutput: (title: string, content: string) => void }) {
  return (
    <div class="happy-kanban-detail__iteration">
      <div class="happy-kanban-detail__iteration-header">
        <span class="happy-kanban-detail__iteration-num">
          迭代 #{props.iter.iterationNum}
        </span>
        <span class="happy-kanban-detail__iteration-duration">
          {formatDuration(props.iter.durationMs)}
        </span>
      </div>
      <div class="happy-kanban-detail__iteration-time">
        {formatDate(props.iter.startedAt)}
        {props.iter.completedAt ? ` → ${formatDate(props.iter.completedAt)}` : ' (运行中)'}
      </div>
      <Show when={props.iter.output}>
        <div
          class="happy-kanban-detail__iteration-output happy-kanban-detail__content-box--clickable"
          onClick={() => props.onShowOutput(`迭代 #${props.iter.iterationNum} 输出`, props.iter.output!)}
        >
          <div class="happy-kanban-detail__content-clamp">
            {props.iter.output}
          </div>
          <span class="happy-kanban-detail__content-expand">点击查看全部</span>
        </div>
      </Show>
    </div>
  );
}

function TaskDetailContent(props: { story: Story }) {
  const kanban = useKanbanContext();
  const [showColumnMenu, setShowColumnMenu] = createSignal(false);
  const [editingField, setEditingField] = createSignal<string | null>(null);
  const [regenerating, setRegenerating] = createSignal<string | null>(null);
  const [modalContent, setModalContent] = createSignal<{ title: string; content: string; isMarkdown?: boolean } | null>(null);

  // 编辑中的内容（本地状态）
  const [requirementsDraft, setRequirementsDraft] = createSignal(props.story.requirements);
  const [acceptanceDraft, setAcceptanceDraft] = createSignal(props.story.userAcceptanceCriteria);

  // Round-based iteration loading
  const [rounds] = createResource(
    () => props.story.id,
    (id) => fetchStoryRounds(id)
  );
  const [expandedRounds, setExpandedRounds] = createSignal<Set<number>>(new Set());
  const [roundIterations, setRoundIterations] = createSignal<Record<number, Iteration[]>>({});
  const [loadingRound, setLoadingRound] = createSignal<number | null>(null);

  // Auto-expand the latest round when rounds load
  createEffect(() => {
    const roundList = rounds();
    if (roundList && roundList.length > 0) {
      const latestRound = Math.max(...roundList.map(r => r.round));
      setExpandedRounds(new Set([latestRound]));
      // Pre-load latest round's iterations
      loadRoundIterations(latestRound);
    }
  });

  const loadRoundIterations = async (round: number) => {
    if (roundIterations()[round]) return; // already cached
    setLoadingRound(round);
    try {
      const iters = await fetchStoryIterationsByRound(props.story.id, round);
      setRoundIterations((prev) => ({ ...prev, [round]: iters }));
    } catch (err) {
      console.error('Failed to load round iterations:', err);
    } finally {
      setLoadingRound(null);
    }
  };

  const toggleRound = (round: number) => {
    setExpandedRounds((prev) => {
      const next = new Set(prev);
      if (next.has(round)) {
        next.delete(round);
      } else {
        next.add(round);
        loadRoundIterations(round);
      }
      return next;
    });
  };

  // Flat iterations fallback for single round
  const [iterations] = createResource(
    () => {
      const roundList = rounds();
      // Only load flat if single round (round=0 only)
      if (roundList && roundList.length <= 1) return props.story.id;
      return undefined;
    },
    (id) => fetchStoryIterations(id)
  );

  // Get the column for this story based on its phase
  const columnId = () => PHASE_TO_COLUMN[props.story.phase];
  const columnConfig = () => KANBAN_COLUMN_CONFIG[columnId()];
  const phaseConfig = () => PHASE_CONFIG[props.story.phase];

  const handleColumnChange = (column: KanbanColumnId) => {
    kanban.moveStoryToColumn(props.story.id, column);
    setShowColumnMenu(false);
  };

  const handlePriorityChange = (priority: Priority) => {
    kanban.updateStoryPriority(props.story.id, priority);
  };

  const startEditing = (field: string) => {
    setEditingField(field);
    // 初始化草稿
    if (field === 'requirements') {
      setRequirementsDraft(props.story.requirements);
    } else if (field === 'userAcceptanceCriteria') {
      setAcceptanceDraft(props.story.userAcceptanceCriteria);
    }
  };

  const saveRequirements = async () => {
    await kanban.updateStory(props.story.id, { requirements: requirementsDraft() });
    setEditingField(null);
  };

  const saveAcceptance = async () => {
    await kanban.updateStory(props.story.id, { userAcceptanceCriteria: acceptanceDraft() });
    setEditingField(null);
  };

  const cancelEdit = () => {
    setEditingField(null);
  };

  const handleRegeneratePlan = async () => {
    setRegenerating('plan');
    try {
      await regeneratePlan(props.story.id);
      kanban.refreshStories();
    } catch (err) {
      console.error('Failed to regenerate plan:', err);
    } finally {
      setRegenerating(null);
    }
  };

  const handleRegenerateAcceptance = async () => {
    setRegenerating('acceptance');
    try {
      await regenerateAcceptance(props.story.id);
      kanban.refreshStories();
    } catch (err) {
      console.error('Failed to regenerate acceptance:', err);
    } finally {
      setRegenerating(null);
    }
  };

  return (
    <div class="happy-kanban-detail">
      {/* Toolbar */}
      <div class="happy-kanban-detail__toolbar">
        <div class="happy-kanban-detail__status-dropdown">
          <button
            class="happy-kanban-detail__status-btn"
            style={{ '--status-color': columnConfig().color }}
            onClick={() => setShowColumnMenu(!showColumnMenu())}
          >
            <span
              class="happy-kanban-detail__status-dot"
              style={{ background: columnConfig().color }}
            />
            {columnConfig().title}
          </button>
          <Show when={showColumnMenu()}>
            <div class="happy-kanban-detail__status-menu">
              <For each={KANBAN_COLUMNS}>
                {(column) => (
                  <button
                    class="happy-kanban-detail__status-menu-item"
                    onClick={() => handleColumnChange(column)}
                  >
                    <span
                      class="happy-kanban-detail__status-dot"
                      style={{ background: KANBAN_COLUMN_CONFIG[column].color }}
                    />
                    {KANBAN_COLUMN_CONFIG[column].title}
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
      <TabsRoot defaultValue="details" class="happy-kanban-detail__tabs">
        <TabsList class="happy-kanban-detail__tabs-list">
          <TabsTrigger value="details">详情</TabsTrigger>
          <TabsTrigger value="iterations">迭代</TabsTrigger>
        </TabsList>

        {/* Details Tab */}
        <TabsContent value="details" class="happy-kanban-detail__panel">
          <div class="happy-kanban-detail__overview">
            {/* Story ID */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">Story ID</div>
              <div class="happy-kanban-detail__row-content">
                <code style={{ color: 'var(--color-primary)', 'font-weight': '600' }}>
                  {props.story.storyId}
                </code>
              </div>
            </div>

            {/* Title */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">标题</div>
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
              <div class="happy-kanban-detail__row-label">优先级</div>
              <div class="happy-kanban-detail__row-content">
                <div class="happy-kanban-detail__priority-group">
                  <For each={['High', 'Medium', 'Low'] as Priority[]}>
                    {(p) => (
                      <button
                        classList={{
                          'happy-kanban-detail__priority-btn': true,
                          'happy-kanban-detail__priority-btn--active': props.story.priority === p,
                        }}
                        onClick={() => handlePriorityChange(p)}
                      >
                        <span
                          style={{
                            display: 'inline-block',
                            width: '8px',
                            height: '8px',
                            'border-radius': '50%',
                            background: PRIORITY_COLORS[p],
                          }}
                        />
                        {p}
                      </button>
                    )}
                  </For>
                </div>
              </div>
            </div>

            {/* Phase */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">阶段</div>
              <div class="happy-kanban-detail__row-content">
                <span
                  style={{
                    color: phaseConfig().color,
                    'font-weight': '600',
                    'font-size': '0.875rem',
                  }}
                >
                  {phaseConfig().title}
                </span>
              </div>
            </div>

            {/* Round */}
            <Show when={props.story.round > 0}>
              <div class="happy-kanban-detail__row">
                <div class="happy-kanban-detail__row-label">轮次</div>
                <div class="happy-kanban-detail__row-content">
                  <span
                    style={{
                      'font-weight': '600',
                      'font-size': '0.875rem',
                      color: '#8b5cf6',
                    }}
                  >
                    Round {props.story.round}
                  </span>
                </div>
              </div>
            </Show>

            {/* Created */}
            <div class="happy-kanban-detail__row">
              <div class="happy-kanban-detail__row-label">创建时间</div>
              <div
                class="happy-kanban-detail__row-content"
                style={{ 'font-size': '0.875rem', color: 'var(--color-muted-foreground)' }}
              >
                {formatDate(props.story.createdAt)}
              </div>
            </div>

            {/* Divider */}
            <div class="happy-kanban-detail__divider" />

            {/* Requirements - User Input */}
            <div class="happy-kanban-detail__section">
              <div class="happy-kanban-detail__section-header">
                <h4 class="happy-kanban-detail__section-title">📋 用户需求</h4>
                <button
                  class="happy-kanban-detail__edit-btn"
                  onClick={() => startEditing('requirements')}
                >
                  编辑
                </button>
              </div>
              <Show
                when={props.story.requirements}
                fallback={
                  <div class="happy-kanban-detail__content-box happy-kanban-detail__content-box--empty">
                    暂无需求描述，点击编辑添加
                  </div>
                }
              >
                <div class="happy-kanban-detail__content-box">
                  <MarkdownViewer content={props.story.requirements} />
                </div>
              </Show>
            </div>

            {/* User Acceptance Criteria - User Input */}
            <div class="happy-kanban-detail__section">
              <div class="happy-kanban-detail__section-header">
                <h4 class="happy-kanban-detail__section-title">✅ 验收标准（用户）</h4>
                <button
                  class="happy-kanban-detail__edit-btn"
                  onClick={() => startEditing('userAcceptanceCriteria')}
                >
                  编辑
                </button>
              </div>
              <Show
                when={props.story.userAcceptanceCriteria}
                fallback={
                  <div class="happy-kanban-detail__content-box happy-kanban-detail__content-box--empty">
                    暂无验收标准，点击编辑添加
                  </div>
                }
              >
                <div class="happy-kanban-detail__content-box">
                  <MarkdownViewer content={props.story.userAcceptanceCriteria} />
                </div>
              </Show>
            </div>

            {/* Detailed Plan - AI Generated */}
            <div class="happy-kanban-detail__section">
              <div class="happy-kanban-detail__section-header">
                <h4 class="happy-kanban-detail__section-title">🤖 详细计划（AI生成）</h4>
                <button
                  class="happy-kanban-detail__regenerate-btn"
                  onClick={handleRegeneratePlan}
                  disabled={regenerating() !== null || !props.story.requirements}
                  title={!props.story.requirements ? '请先填写用户需求' : '重新生成计划'}
                >
                  {regenerating() === 'plan' ? '生成中...' : '重新生成'}
                </button>
              </div>
              <Show
                when={props.story.detailedPlan}
                fallback={
                  <div class="happy-kanban-detail__content-box happy-kanban-detail__content-box--ai happy-kanban-detail__content-box--empty">
                    暂无详细计划，点击"重新生成"由AI生成
                  </div>
                }
              >
                <div
                  class="happy-kanban-detail__content-box happy-kanban-detail__content-box--ai happy-kanban-detail__content-box--clickable"
                  onClick={() => setModalContent({ title: '🤖 详细计划（AI生成）', content: props.story.detailedPlan, isMarkdown: true })}
                >
                  <div class="happy-kanban-detail__content-clamp">
                    <MarkdownViewer content={props.story.detailedPlan} />
                  </div>
                  <span class="happy-kanban-detail__content-expand">点击查看全部</span>
                </div>
              </Show>
            </div>

            {/* Acceptance Criteria - AI Generated */}
            <div class="happy-kanban-detail__section">
              <div class="happy-kanban-detail__section-header">
                <h4 class="happy-kanban-detail__section-title">🎯 细化验收标准（AI生成）</h4>
                <button
                  class="happy-kanban-detail__regenerate-btn"
                  onClick={handleRegenerateAcceptance}
                  disabled={regenerating() !== null || !props.story.detailedPlan}
                  title={!props.story.detailedPlan ? '请先生成详细计划' : '重新生成验收标准'}
                >
                  {regenerating() === 'acceptance' ? '生成中...' : '重新生成'}
                </button>
              </div>
              <Show
                when={props.story.acceptanceCriteria}
                fallback={
                  <div class="happy-kanban-detail__content-box happy-kanban-detail__content-box--ai happy-kanban-detail__content-box--empty">
                    暂无细化验收标准，点击"重新生成"由AI生成
                  </div>
                }
              >
                <div
                  class="happy-kanban-detail__content-box happy-kanban-detail__content-box--ai happy-kanban-detail__content-box--clickable"
                  onClick={() => setModalContent({ title: '🎯 细化验收标准（AI生成）', content: props.story.acceptanceCriteria, isMarkdown: true })}
                >
                  <div class="happy-kanban-detail__content-clamp">
                    <MarkdownViewer content={props.story.acceptanceCriteria} />
                  </div>
                  <span class="happy-kanban-detail__content-expand">点击查看全部</span>
                </div>
              </Show>
            </div>
          </div>
        </TabsContent>

        {/* Iterations Tab */}
        <TabsContent value="iterations" class="happy-kanban-detail__panel">
          <Show
            when={!rounds.loading}
            fallback={
              <div class="happy-kanban-detail__empty">
                <p>加载迭代记录...</p>
              </div>
            }
          >
            <Show
              when={rounds()?.length}
              fallback={
                <div class="happy-kanban-detail__empty">
                  <p>暂无迭代记录</p>
                </div>
              }
            >
              {/* Single round: flat list without round header */}
              <Show
                when={(rounds()?.length ?? 0) > 1}
                fallback={
                  <div class="happy-kanban-detail__iterations">
                    <Show
                      when={!iterations.loading && iterations()?.length}
                      fallback={
                        <div class="happy-kanban-detail__empty">
                          <p>加载中...</p>
                        </div>
                      }
                    >
                      <For each={iterations()}>
                        {(iter) => <IterationItem iter={iter} onShowOutput={(title, content) => setModalContent({ title, content })} />}
                      </For>
                    </Show>
                  </div>
                }
              >
                {/* Multiple rounds: collapsible groups */}
                <div class="happy-kanban-detail__iterations">
                  <For each={[...(rounds() ?? [])].reverse()}>
                    {(roundSummary) => (
                      <div class="happy-kanban-detail__round">
                        <button
                          class="happy-kanban-detail__round-header"
                          onClick={() => toggleRound(roundSummary.round)}
                        >
                          <span class="happy-kanban-detail__round-toggle">
                            {expandedRounds().has(roundSummary.round) ? '▾' : '▸'}
                          </span>
                          <span class="happy-kanban-detail__round-title">
                            Round {roundSummary.round}{roundSummary.round === props.story.round ? ' (当前)' : ''}
                          </span>
                          <span class="happy-kanban-detail__round-count">
                            {roundSummary.iterationCount} 次迭代
                          </span>
                        </button>
                        <Show when={expandedRounds().has(roundSummary.round)}>
                          <div class="happy-kanban-detail__round-content">
                            <Show
                              when={loadingRound() !== roundSummary.round && roundIterations()[roundSummary.round]}
                              fallback={
                                <div class="happy-kanban-detail__empty" style={{ padding: '16px 0' }}>
                                  <p>加载中...</p>
                                </div>
                              }
                            >
                              <For each={roundIterations()[roundSummary.round]}>
                                {(iter) => <IterationItem iter={iter} onShowOutput={(title, content) => setModalContent({ title, content })} />}
                              </For>
                            </Show>
                          </div>
                        </Show>
                      </div>
                    )}
                  </For>
                </div>
              </Show>
            </Show>
          </Show>
        </TabsContent>
      </TabsRoot>

      <ContentModal
        open={!!modalContent()}
        title={modalContent()?.title ?? ''}
        content={modalContent()?.content ?? ''}
        isMarkdown={modalContent()?.isMarkdown}
        onClose={() => setModalContent(null)}
      />

      <EditorModal
        open={editingField() === 'requirements'}
        title="📋 编辑用户需求"
        content={requirementsDraft()}
        onChange={setRequirementsDraft}
        onSave={saveRequirements}
        onClose={cancelEdit}
        placeholder="输入用户需求（支持 Markdown 格式）..."
      />

      <EditorModal
        open={editingField() === 'userAcceptanceCriteria'}
        title="✅ 编辑验收标准"
        content={acceptanceDraft()}
        onChange={setAcceptanceDraft}
        onSave={saveAcceptance}
        onClose={cancelEdit}
        placeholder="输入验收标准（支持 Markdown 格式）..."
      />
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
