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
  const [prdJson, setPrdJson] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);

    const nameVal = name().trim();

    if (!nameVal) {
      setError('Project name is required');
      return;
    }

    setSubmitting(true);
    try {
      // 工作目录将由后端自动生成：根目录 + 项目名称
      await props.onSubmit(nameVal, '', prdJson());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create project');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div class="morty-create-project-form">
      <div class="morty-create-project-form__overlay" onClick={props.onCancel} />
      <div class="morty-create-project-form__modal">
        <div class="morty-create-project-form__header">
          <h2>Create New Project</h2>
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
            <label class="morty-create-project-form__label">Project Name *</label>
            <Input
              placeholder="My Project"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              disabled={submitting()}
            />
            <p class="morty-create-project-form__hint">
              Working directory will be auto-generated under projects root
            </p>
          </div>

          <div class="morty-create-project-form__field">
            <label class="morty-create-project-form__label">PRD (JSON)</label>
            <textarea
              class="morty-create-project-form__textarea"
              placeholder='{"requirements": "..."}'
              value={prdJson()}
              onInput={(e) => setPrdJson(e.currentTarget.value)}
              disabled={submitting()}
              rows={6}
            />
          </div>

          <div class="morty-create-project-form__actions">
            <Button
              variant="ghost"
              type="button"
              onClick={props.onCancel}
              disabled={submitting()}
            >
              Cancel
            </Button>
            <Button
              variant="primary"
              type="submit"
              disabled={submitting()}
            >
              {submitting() ? 'Creating...' : 'Create Project'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
