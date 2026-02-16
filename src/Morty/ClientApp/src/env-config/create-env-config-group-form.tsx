/**
 * 创建/编辑环境配置组表单组件
 */

import { createSignal, For, Show, Index } from 'solid-js';
import type { EnvConfigGroup, CreateEnvVariableDto } from '../types';
import { createEnvConfigGroup, updateEnvConfigGroup } from '../api/client';
import { Input } from '@ui/input';
import { Button } from '@ui/button';

interface CreateEnvConfigGroupFormProps {
  editingGroup?: EnvConfigGroup | null;
  onSuccess: () => void;
  onCancel: () => void;
}

export function CreateEnvConfigGroupForm(props: CreateEnvConfigGroupFormProps) {
  const isEditing = () => !!props.editingGroup;

  const [name, setName] = createSignal(props.editingGroup?.name || '');
  const [description, setDescription] = createSignal(props.editingGroup?.description || '');
  const [variables, setVariables] = createSignal<CreateEnvVariableDto[]>(
    props.editingGroup?.variables.map(v => ({
      key: v.key,
      value: v.value,
      isRequired: v.isRequired,
      defaultValue: v.defaultValue || undefined,
    })) || []
  );
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const addVariable = () => {
    setVariables([
      ...variables(),
      { key: '', value: '', isRequired: false, defaultValue: undefined }
    ]);
  };

  const removeVariable = (index: number) => {
    setVariables(variables().filter((_, i) => i !== index));
  };

  const updateVariable = (
    index: number,
    field: keyof CreateEnvVariableDto,
    value: string | boolean
  ) => {
    const newVars = [...variables()];
    newVars[index] = { ...newVars[index], [field]: value };
    setVariables(newVars);
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);

    const nameVal = name().trim();
    if (!nameVal) {
      setError('配置组名称不能为空');
      return;
    }

    setSubmitting(true);
    try {
      const dto = {
        name: nameVal,
        description: description().trim(),
        variables: variables().filter(v => v.key.trim() !== ''),
      };

      if (isEditing()) {
        await updateEnvConfigGroup(props.editingGroup!.id, dto);
      } else {
        await createEnvConfigGroup(dto);
      }

      props.onSuccess();
    } catch (err) {
      setError(err instanceof Error ? err.message : '操作失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div class="morty-create-env-config-group-form">
      <div class="morty-create-env-config-group-form__overlay" onClick={props.onCancel} />
      <div class="morty-create-env-config-group-form__modal">
        <div class="morty-create-env-config-group-form__header">
          <h2>{isEditing() ? '编辑配置组' : '创建配置组'}</h2>
          <button
            class="morty-create-env-config-group-form__close"
            onClick={props.onCancel}
          >
            ✕
          </button>
        </div>

        <form class="morty-create-env-config-group-form__body" onSubmit={handleSubmit}>
          <Show when={error()}>
            <div class="morty-create-env-config-group-form__error">{error()}</div>
          </Show>

          <div class="morty-create-env-config-group-form__field">
            <label class="morty-create-env-config-group-form__label">配置组名称 *</label>
            <Input
              placeholder="例如: Claude API 生产环境"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              disabled={submitting()}
            />
          </div>

          <div class="morty-create-env-config-group-form__field">
            <label class="morty-create-env-config-group-form__label">描述</label>
            <Input
              placeholder="配置组的说明"
              value={description()}
              onInput={(e) => setDescription(e.currentTarget.value)}
              disabled={submitting()}
            />
          </div>

          <div class="morty-create-env-config-group-form__section">
            <div class="morty-create-env-config-group-form__section-header">
              <h3 class="morty-create-env-config-group-form__section-title">环境变量</h3>
              <button
                type="button"
                class="morty-create-env-config-group-form__add-btn"
                onClick={addVariable}
                disabled={submitting()}
              >
                + 添加变量
              </button>
            </div>

            <div class="morty-create-env-config-group-form__variables">
              <Index each={variables()}>
                {(variable, idx) => (
                  <div class="morty-create-env-config-group-form__variable">
                    <div class="morty-create-env-config-group-form__variable-row">
                      <input
                        class="happy-input"
                        placeholder="变量名 (KEY)"
                        value={variable().key}
                        onInput={(e) => updateVariable(idx, 'key', e.currentTarget.value)}
                        disabled={submitting()}
                      />
                      <input
                        class="happy-input"
                        placeholder="值 (VALUE)"
                        value={variable().value}
                        onInput={(e) => updateVariable(idx, 'value', e.currentTarget.value)}
                        disabled={submitting()}
                      />
                      <button
                        type="button"
                        class="morty-create-env-config-group-form__remove-btn"
                        onClick={() => removeVariable(idx)}
                        disabled={submitting()}
                      >
                        ✕
                      </button>
                    </div>
                    <div class="morty-create-env-config-group-form__variable-options">
                      <label class="morty-create-env-config-group-form__checkbox">
                        <input
                          type="checkbox"
                          checked={variable().isRequired}
                          onChange={(e) => updateVariable(idx, 'isRequired', e.currentTarget.checked)}
                          disabled={submitting()}
                        />
                        必需
                      </label>
                      <input
                        class="happy-input"
                        placeholder="默认值（可选）"
                        value={variable().defaultValue || ''}
                        onInput={(e) => updateVariable(idx, 'defaultValue', e.currentTarget.value)}
                        disabled={submitting()}
                      />
                    </div>
                  </div>
                )}
              </Index>
              <Show when={variables().length === 0}>
                <div class="morty-create-env-config-group-form__empty">
                  暂无环境变量，点击上方"添加变量"按钮添加
                </div>
              </Show>
            </div>
          </div>

          <div class="morty-create-env-config-group-form__actions">
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
              {submitting() ? '保存中...' : (isEditing() ? '保存修改' : '创建配置组')}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
