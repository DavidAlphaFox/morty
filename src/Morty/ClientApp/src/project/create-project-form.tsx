/**
 * 创建项目表单组件
 */

import { createSignal, Show } from 'solid-js';
import { Input } from '@ui/input';
import { Button } from '@ui/button';

interface CreateProjectFormProps {
  onSubmit: (name: string, workingDirectory: string, prdJson: string) => Promise<void>;
  onCancel: () => void;
}

export function CreateProjectForm(props: CreateProjectFormProps) {
  const [name, setName] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);

    const nameVal = name().trim();

    if (!nameVal) {
      setError('请输入项目名称');
      return;
    }

    setSubmitting(true);
    try {
      // 工作目录将由后端自动生成：根目录 + 项目名称
      await props.onSubmit(nameVal, '', '');
    } catch (err) {
      setError(err instanceof Error ? err.message : '创建项目失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div class="morty-create-project-form">
      <div class="morty-create-project-form__overlay" onClick={props.onCancel} />
      <div class="morty-create-project-form__modal">
        <div class="morty-create-project-form__header">
          <h2>新建项目</h2>
          <button
            class="morty-create-project-form__close"
            onClick={props.onCancel}
          >
            ✕
          </button>
        </div>

        <form class="morty-create-project-form__body" onSubmit={handleSubmit}>
          <Show when={error()}>
            <div class="morty-create-project-form__error">{error()}</div>
          </Show>

          <div class="morty-create-project-form__field">
            <label class="morty-create-project-form__label">项目名称 *</label>
            <Input
              placeholder="我的项目"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              disabled={submitting()}
            />
            <p class="morty-create-project-form__hint">
              工作目录将在项目根目录下自动生成
            </p>
          </div>

          <div class="morty-create-project-form__actions">
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
              {submitting() ? '创建中...' : '创建项目'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
