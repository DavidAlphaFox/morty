/**
 * 创建故事模态对话框
 * 支持填写标题、需求、验收标准和选择父任务
 */

import { createSignal, Show, For, createEffect } from 'solid-js';
import { Button } from '@ui/button';
import { Input } from '@ui/input';
import { MarkdownEditor } from '@ui/markdown-editor';
import { useKanbanContext } from './kanban-context';
import type { Priority, Story, KanbanColumnId } from '../types';
import { KANBAN_COLUMN_CONFIG } from '../types';

interface CreateStoryModalProps {
  columnId: KanbanColumnId;
  onSubmit: (data: CreateStoryData) => Promise<void>;
  onCancel: () => void;
}

export interface CreateStoryData {
  title: string;
  priority: Priority;
  requirements: string;
  userAcceptanceCriteria: string;
  dependencies: number[];
}

const PRIORITY_OPTIONS: { value: Priority; label: string; color: string }[] = [
  { value: 'High', label: 'High', color: '#ef4444' },
  { value: 'Medium', label: 'Medium', color: '#f59e0b' },
  { value: 'Low', label: 'Low', color: '#22c55e' },
];

export function CreateStoryModal(props: CreateStoryModalProps) {
  const kanban = useKanbanContext();

  // 表单状态
  const [title, setTitle] = createSignal('');
  const [priority, setPriority] = createSignal<Priority>('Medium');
  const [requirements, setRequirements] = createSignal('');
  const [acceptanceCriteria, setAcceptanceCriteria] = createSignal('');
  const [selectedDependencies, setSelectedDependencies] = createSignal<number[]>([]);
  const [showDependencySelector, setShowDependencySelector] = createSignal(false);

  // UI状态
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);
  const [activeTab, setActiveTab] = createSignal<'basic' | 'requirements' | 'dependencies'>('basic');

  // 获取可选的父任务列表（排除已完成和当前列中的故事）
  const availableParentStories = () => {
    return kanban.stories().filter(s =>
      s.phase !== 'Completed' &&
      s.phase !== 'Failed' &&
      !selectedDependencies().includes(s.id)
    );
  };

  // 获取已选择的父任务
  const selectedParentStories = () => {
    return kanban.stories().filter(s => selectedDependencies().includes(s.id));
  };

  const toggleDependency = (storyId: number) => {
    setSelectedDependencies(prev => {
      if (prev.includes(storyId)) {
        return prev.filter(id => id !== storyId);
      } else {
        return [...prev, storyId];
      }
    });
  };

  const removeDependency = (storyId: number) => {
    setSelectedDependencies(prev => prev.filter(id => id !== storyId));
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);

    const titleVal = title().trim();
    if (!titleVal) {
      setError('请输入故事标题');
      setActiveTab('basic');
      return;
    }

    setSubmitting(true);
    try {
      await props.onSubmit({
        title: titleVal,
        priority: priority(),
        requirements: requirements(),
        userAcceptanceCriteria: acceptanceCriteria(),
        dependencies: selectedDependencies(),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : '创建失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div class="morty-modal morty-modal--large">
      <div class="morty-modal__overlay" />
      <div class="morty-modal__content">
        <div class="morty-modal__header">
          <h2 class="morty-modal__title">创建新故事</h2>
          <span
            class="morty-modal__column-badge"
            style={{ background: KANBAN_COLUMN_CONFIG[props.columnId].color + '20', color: KANBAN_COLUMN_CONFIG[props.columnId].color }}
          >
            {KANBAN_COLUMN_CONFIG[props.columnId].title}
          </span>
          <button class="morty-modal__close" onClick={props.onCancel}>✕</button>
        </div>

        {/* Tab Navigation */}
        <div class="morty-modal__tabs">
          <button
            class="morty-modal__tab"
            classList={{ 'morty-modal__tab--active': activeTab() === 'basic' }}
            onClick={() => setActiveTab('basic')}
          >
            基本信息
          </button>
          <button
            class="morty-modal__tab"
            classList={{ 'morty-modal__tab--active': activeTab() === 'requirements' }}
            onClick={() => setActiveTab('requirements')}
          >
            需求与验收
          </button>
          <button
            class="morty-modal__tab"
            classList={{ 'morty-modal__tab--active': activeTab() === 'dependencies' }}
            onClick={() => setActiveTab('dependencies')}
          >
            父任务
            <Show when={selectedDependencies().length > 0}>
              <span class="morty-modal__tab-badge">{selectedDependencies().length}</span>
            </Show>
          </button>
        </div>

        <form class="morty-modal__body" onSubmit={handleSubmit}>
          <Show when={error()}>
            <div class="morty-modal__error">{error()}</div>
          </Show>

          {/* Basic Info Tab */}
          <Show when={activeTab() === 'basic'}>
            <div class="morty-modal__tab-content">
              <div class="morty-modal__field">
                <label class="morty-modal__label">故事标题 *</label>
                <Input
                  placeholder="输入故事标题..."
                  value={title()}
                  onInput={(e) => setTitle(e.currentTarget.value)}
                  disabled={submitting()}
                  autofocus
                />
              </div>

              <div class="morty-modal__field">
                <label class="morty-modal__label">优先级</label>
                <div class="morty-modal__priority-group">
                  <For each={PRIORITY_OPTIONS}>
                    {(opt) => (
                      <button
                        type="button"
                        classList={{
                          'morty-modal__priority-btn': true,
                          'morty-modal__priority-btn--active': priority() === opt.value,
                        }}
                        onClick={() => setPriority(opt.value)}
                        disabled={submitting()}
                      >
                        <span
                          class="morty-modal__priority-dot"
                          style={{ background: opt.color }}
                        />
                        {opt.label}
                      </button>
                    )}
                  </For>
                </div>
              </div>
            </div>
          </Show>

          {/* Requirements Tab */}
          <Show when={activeTab() === 'requirements'}>
            <div class="morty-modal__tab-content">
              <div class="morty-modal__field">
                <label class="morty-modal__label">📋 用户需求</label>
                <p class="morty-modal__hint">描述这个任务需要完成什么功能或目标</p>
                <div class="morty-modal__editor-wrapper">
                  <MarkdownEditor
                    content={requirements()}
                    onChange={setRequirements}
                    placeholder="输入用户需求（支持 Markdown 格式）..."
                    minHeight="150px"
                  />
                </div>
              </div>

              <div class="morty-modal__field">
                <label class="morty-modal__label">✅ 验收标准</label>
                <p class="morty-modal__hint">定义如何验证这个任务已完成</p>
                <div class="morty-modal__editor-wrapper">
                  <MarkdownEditor
                    content={acceptanceCriteria()}
                    onChange={setAcceptanceCriteria}
                    placeholder="输入验收标准（支持 Markdown 格式）..."
                    minHeight="120px"
                  />
                </div>
              </div>
            </div>
          </Show>

          {/* Dependencies Tab */}
          <Show when={activeTab() === 'dependencies'}>
            <div class="morty-modal__tab-content">
              <div class="morty-modal__field">
                <label class="morty-modal__label">父任务（可选）</label>
                <p class="morty-modal__hint">选择这个任务依赖的其他任务，依赖任务完成后才会开始此任务</p>

                {/* 已选择的父任务 */}
                <Show when={selectedDependencies().length > 0}>
                  <div class="morty-dependencies__selected">
                    <div class="morty-dependencies__selected-label">已选择：</div>
                    <div class="morty-dependencies__selected-list">
                      <For each={selectedParentStories()}>
                        {(story) => (
                          <div class="morty-dependencies__chip">
                            <span class="morty-dependencies__chip-id">{story.storyId}</span>
                            <span class="morty-dependencies__chip-title">{story.title}</span>
                            <button
                              type="button"
                              class="morty-dependencies__chip-remove"
                              onClick={() => removeDependency(story.id)}
                            >
                              ✕
                            </button>
                          </div>
                        )}
                      </For>
                    </div>
                  </div>
                </Show>

                {/* 可选择的任务列表 */}
                <Show
                  when={availableParentStories().length > 0}
                  fallback={
                    <div class="morty-dependencies__empty">
                      没有可选择的父任务
                    </div>
                  }
                >
                  <div class="morty-dependencies__list">
                    <For each={availableParentStories()}>
                      {(story) => (
                        <div
                          class="morty-dependencies__item"
                          onClick={() => toggleDependency(story.id)}
                        >
                          <div class="morty-dependencies__item-checkbox">
                            <Show when={selectedDependencies().includes(story.id)}>
                              ✓
                            </Show>
                          </div>
                          <div class="morty-dependencies__item-content">
                            <span class="morty-dependencies__item-id">{story.storyId}</span>
                            <span class="morty-dependencies__item-title">{story.title}</span>
                          </div>
                        </div>
                      )}
                    </For>
                  </div>
                </Show>
              </div>
            </div>
          </Show>

          <div class="morty-modal__actions">
            <Button
              variant="ghost"
              type="button"
              onClick={props.onCancel}
              disabled={submitting()}
            >
              取消
            </Button>
            <Button
              variant="primary"
              type="submit"
              disabled={submitting()}
            >
              {submitting() ? '创建中...' : '创建故事'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
