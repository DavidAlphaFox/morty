/**
 * 环境变量规则列表组件
 * 显示项目的所有环境变量规则
 */

import { createSignal, createResource, Show, For } from 'solid-js';
import type { EnvConfigRule } from '../types';
import { fetchEnvConfigRules, deleteEnvConfigRule } from '../api/client';
import { CreateEnvConfigRuleForm } from './create-env-config-rule-form';
import { PHASE_CONFIG } from '../types';

interface EnvConfigRulesListProps {
  projectId: number;
}

export function EnvConfigRulesList(props: EnvConfigRulesListProps) {
  const [rules, { refetch }] = createResource(() => fetchEnvConfigRules(props.projectId));
  const [showCreateForm, setShowCreateForm] = createSignal(false);

  const handleDelete = async (id: number) => {
    if (!confirm('确定要删除这个规则吗？')) return;

    try {
      await deleteEnvConfigRule(props.projectId, id);
      refetch();
    } catch (err) {
      console.error('Failed to delete env config rule:', err);
      alert('删除失败');
    }
  };

  const handleCreateSuccess = () => {
    setShowCreateForm(false);
    refetch();
  };

  return (
    <div class="morty-env-config-rules-list">
      <div class="morty-env-config-rules-list__header">
        <h3 class="morty-env-config-rules-list__title">环境变量规则</h3>
        <button
          class="morty-env-config-rules-list__create-btn"
          onClick={() => setShowCreateForm(true)}
        >
          + 新建规则
        </button>
      </div>

      <p class="morty-env-config-rules-list__hint">
        配置在特定阶段和标签下使用的环境变量配置组
      </p>

      <Show when={showCreateForm()}>
        <CreateEnvConfigRuleForm
          projectId={props.projectId}
          onSuccess={handleCreateSuccess}
          onCancel={() => setShowCreateForm(false)}
        />
      </Show>

      <Show
        when={!rules.loading}
        fallback={<div class="morty-env-config-rules-list__loading">加载中...</div>}
      >
        <Show
          when={rules()?.length}
          fallback={
            <div class="morty-env-config-rules-list__empty">
              <div class="morty-env-config-rules-list__empty-text">暂无规则</div>
              <div class="morty-env-config-rules-list__empty-hint">
                创建规则来自动应用环境配置
              </div>
            </div>
          }
        >
          <div class="morty-env-config-rules-list__list">
            <For each={rules()}>
              {(rule) => (
                <EnvConfigRuleCard
                  rule={rule}
                  onDelete={() => handleDelete(rule.id)}
                />
              )}
            </For>
          </div>
        </Show>
      </Show>
    </div>
  );
}

function EnvConfigRuleCard(props: { rule: EnvConfigRule; onDelete: () => void }) {
  const formatPhase = (phase: string | undefined) => {
    if (!phase) return '任何阶段';
    return PHASE_CONFIG[phase as keyof typeof PHASE_CONFIG]?.title || phase;
  };

  return (
    <div class="morty-env-config-rule-card">
      <div class="morty-env-config-rule-card__header">
        <div class="morty-env-config-rule-card__config-name">
          {props.rule.envConfigGroup?.name || `配置组 #${props.rule.envConfigGroupId}`}
        </div>
        <div class="morty-env-config-rule-card__priority">
          优先级: {props.rule.priority}
        </div>
      </div>

      <div class="morty-env-config-rule-card__conditions">
        <div class="morty-env-config-rule-card__phase">
          <span class="morty-env-config-rule-card__label">阶段:</span>
          {formatPhase(props.rule.fromPhase)} → {formatPhase(props.rule.toPhase)}
        </div>

        <Show when={props.rule.tags.length > 0}>
          <div class="morty-env-config-rule-card__tags">
            <span class="morty-env-config-rule-card__label">标签:</span>
            <For each={props.rule.tags}>
              {(tag) => (
                <span class="morty-env-config-rule-card__tag">{tag}</span>
              )}
            </For>
          </div>
        </Show>
        <Show when={props.rule.tags.length === 0}>
          <div class="morty-env-config-rule-card__tags">
            <span class="morty-env-config-rule-card__label">标签:</span>
            <span class="morty-env-config-rule-card__tag-any">全部匹配</span>
          </div>
        </Show>
      </div>

      <Show when={props.rule.envConfigGroup}>
        <div class="morty-env-config-rule-card__variables">
          <span class="morty-env-config-rule-card__label">环境变量:</span>
          <For each={props.rule.envConfigGroup!.variables}>
            {(variable) => (
              <span class="morty-env-config-rule-card__variable">
                {variable.key}
              </span>
            )}
          </For>
          <Show when={props.rule.envConfigGroup!.variables.length === 0}>
            <span class="morty-env-config-rule-card__no-variables">无变量</span>
          </Show>
        </div>
      </Show>

      <div class="morty-env-config-rule-card__footer">
        <button
          class="morty-env-config-rule-card__delete"
          onClick={props.onDelete}
        >
          删除
        </button>
      </div>
    </div>
  );
}
