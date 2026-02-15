/**
 * 创建环境变量规则表单组件
 */

import { createSignal, createResource, Show, For } from 'solid-js';
import type { StoryPhase } from '../types';
import { createEnvConfigRule, fetchEnvConfigGroups } from '../api/client';
import { STORY_PHASES } from '../types';
import { Input } from '@ui/input';
import { Button } from '@ui/button';

interface CreateEnvConfigRuleFormProps {
  projectId: number;
  onSuccess: () => void;
  onCancel: () => void;
}

export function CreateEnvConfigRuleForm(props: CreateEnvConfigRuleFormProps) {
  const [envConfigGroups] = createResource(fetchEnvConfigGroups);

  const [envConfigGroupId, setEnvConfigGroupId] = createSignal<number | null>(null);
  const [fromPhase, setFromPhase] = createSignal<StoryPhase | null>(null);
  const [toPhase, setToPhase] = createSignal<StoryPhase | null>(null);
  const [tagsInput, setTagsInput] = createSignal('');
  const [priority, setPriority] = createSignal(0);
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);

    if (!envConfigGroupId()) {
      setError('请选择环境配置组');
      return;
    }

    setSubmitting(true);
    try {
      const tags = tagsInput()
        .split(',')
        .map(t => t.trim())
        .filter(t => t !== '');

      await createEnvConfigRule(props.projectId, {
        projectId: props.projectId,
        envConfigGroupId: envConfigGroupId()!,
        fromPhase: fromPhase() || undefined,
        toPhase: toPhase() || undefined,
        tags,
        priority: priority(),
      });

      props.onSuccess();
    } catch (err) {
      setError(err instanceof Error ? err.message : '创建失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div class="morty-create-env-config-rule-form">
      <div class="morty-create-env-config-rule-form__overlay" onClick={props.onCancel} />
      <div class="morty-create-env-config-rule-form__modal">
        <div class="morty-create-env-config-rule-form__header">
          <h2>创建环境变量规则</h2>
          <button
            class="morty-create-env-config-rule-form__close"
            onClick={props.onCancel}
          >
            ✕
          </button>
        </div>

        <form class="morty-create-env-config-rule-form__body" onSubmit={handleSubmit}>
          <Show when={error()}>
            <div class="morty-create-env-config-rule-form__error">{error()}</div>
          </Show>

          <div class="morty-create-env-config-rule-form__field">
            <label class="morty-create-env-config-rule-form__label">环境配置组 *</label>
            <select
              class="morty-create-env-config-rule-form__select"
              onChange={(e) => setEnvConfigGroupId(parseInt(e.currentTarget.value))}
              disabled={submitting() || envConfigGroups.loading}
            >
              <option value="">请选择配置组</option>
              <For each={envConfigGroups()}>
                {(group) => (
                  <option value={group.id}>{group.name}</option>
                )}
              </For>
            </select>
          </div>

          <div class="morty-create-env-config-rule-form__field">
            <label class="morty-create-env-config-rule-form__label">
              起始阶段（留空表示任意阶段）
            </label>
            <select
              class="morty-create-env-config-rule-form__select"
              onChange={(e) =>
                setFromPhase(e.currentTarget.value === '' ? null : e.currentTarget.value as StoryPhase)
              }
              disabled={submitting()}
            >
              <option value="">任意阶段</option>
              <For each={STORY_PHASES}>
                {(phase) => <option value={phase}>{phase}</option>}
              </For>
            </select>
          </div>

          <div class="morty-create-env-config-rule-form__field">
            <label class="morty-create-env-config-rule-form__label">
              目标阶段（留空表示任意阶段）
            </label>
            <select
              class="morty-create-env-config-rule-form__select"
              onChange={(e) =>
                setToPhase(e.currentTarget.value === '' ? null : e.currentTarget.value as StoryPhase)
              }
              disabled={submitting()}
            >
              <option value="">任意阶段</option>
              <For each={STORY_PHASES}>
                {(phase) => <option value={phase}>{phase}</option>}
              </For>
            </select>
          </div>

          <div class="morty-create-env-config-rule-form__field">
            <label class="morty-create-env-config-rule-form__label">
              标签（多个标签用逗号分隔，留空表示匹配所有）
            </label>
            <Input
              placeholder="例如: frontend,api"
              value={tagsInput()}
              onInput={(e) => setTagsInput(e.currentTarget.value)}
              disabled={submitting()}
            />
          </div>

          <div class="morty-create-env-config-rule-form__field">
            <label class="morty-create-env-config-rule-form__label">
              优先级（数字越大优先级越高）
            </label>
            <Input
              type="number"
              placeholder="0"
              value={priority().toString()}
              onInput={(e) => setPriority(parseInt(e.currentTarget.value) || 0)}
              disabled={submitting()}
            />
          </div>

          <div class="morty-create-env-config-rule-form__actions">
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
              {submitting() ? '创建中...' : '创建规则'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
