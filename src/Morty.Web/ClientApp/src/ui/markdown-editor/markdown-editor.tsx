/**
 * Markdown 编辑器组件
 * 基于 Tiptap 构建，支持富文本编辑和 Markdown
 */

import { createEffect, onCleanup, Show, createSignal, onMount } from 'solid-js';
import { Editor } from '@tiptap/core';
import type { Extension } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Placeholder from '@tiptap/extension-placeholder';

interface MarkdownEditorProps {
  content: string;
  onChange: (content: string) => void;
  placeholder?: string;
  readonly?: boolean;
  minHeight?: string;
}

export function MarkdownEditor(props: MarkdownEditorProps) {
  let editorRef: HTMLDivElement | undefined;

  const [editor, setEditor] = createSignal<Editor | undefined>(undefined);

  // 初始化编辑器
  onMount(() => {
    if (!editorRef) return;

    const editorInstance = new Editor({
      element: editorRef,
      extensions: [
        StarterKit.configure({
          heading: {
            levels: [1, 2, 3],
          },
          bulletList: {
            keepMarks: true,
            keepAttributes: false,
          },
          orderedList: {
            keepMarks: true,
            keepAttributes: false,
          },
        }),
        Placeholder.configure({
          placeholder: props.placeholder || '开始输入...',
        }),
      ],
      content: props.content,
      editable: !props.readonly,
      editorProps: {
        attributes: {
          class: 'morty-markdown-editor__content',
          ...(props.minHeight ? { style: `min-height: ${props.minHeight}` } : {}),
        },
      },
      onUpdate: ({ editor: e }) => {
        // 使用 Tiptap 的 HTML 输出，但我们希望存储 Markdown 格式
        // 这里先存储 HTML，后续可以扩展支持 Markdown
        const html = e.getHTML();
        // 简单转换为纯文本（去掉HTML标签），作为临时方案
        // 更好的做法是使用 tiptap-markdown 扩展
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = html;
        const text = tempDiv.textContent || tempDiv.innerText || '';
        props.onChange(text);
      },
    });

    setEditor(editorInstance);
  });

  // 当外部内容变化时同步更新编辑器
  createEffect(() => {
    const e = editor();
    if (e && !e.isFocused && props.content !== e.getHTML()) {
      e.commands.setContent(props.content);
    }
  });

  // 更新可编辑状态
  createEffect(() => {
    const e = editor();
    if (e) {
      e.setEditable(!props.readonly);
    }
  });

  // 清理
  onCleanup(() => {
    editor()?.destroy();
  });

  const isActive = (name: string, attrs?: Record<string, unknown>) => {
    const e = editor();
    if (!e) return false;
    return e.isActive(name, attrs);
  };

  const execCommand = (command: (e: Editor) => void) => {
    const e = editor();
    if (e) {
      e.chain().focus();
      command(e);
    }
  };

  return (
    <div
      class="morty-markdown-editor"
      classList={{ 'morty-markdown-editor--readonly': props.readonly }}
    >
      <Show when={!props.readonly}>
        <div class="morty-markdown-editor__toolbar">
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleBold().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('bold') }}
            title="粗体"
          >
            <strong>B</strong>
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleItalic().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('italic') }}
            title="斜体"
          >
            <em>I</em>
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleStrike().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('strike') }}
            title="删除线"
          >
            <s>S</s>
          </button>
          <span class="morty-markdown-editor__divider" />
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleHeading({ level: 1 }).run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('heading', { level: 1 }) }}
            title="标题1"
          >
            H1
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleHeading({ level: 2 }).run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('heading', { level: 2 }) }}
            title="标题2"
          >
            H2
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleHeading({ level: 3 }).run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('heading', { level: 3 }) }}
            title="标题3"
          >
            H3
          </button>
          <span class="morty-markdown-editor__divider" />
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleBulletList().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('bulletList') }}
            title="无序列表"
          >
            • 列表
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleOrderedList().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('orderedList') }}
            title="有序列表"
          >
            1. 列表
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleCodeBlock().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('codeBlock') }}
            title="代码块"
          >
            {'</>'}
          </button>
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().toggleBlockquote().run())}
            classList={{ 'morty-markdown-editor__btn--active': isActive('blockquote') }}
            title="引用"
          >
            "
          </button>
          <span class="morty-markdown-editor__divider" />
          <button
            type="button"
            class="morty-markdown-editor__btn"
            onClick={() => execCommand(e => e.chain().focus().setHorizontalRule().run())}
            title="分割线"
          >
            ―
          </button>
        </div>
      </Show>
      <div ref={editorRef} class="morty-markdown-editor__wrapper" />
    </div>
  );
}

/**
 * Markdown 内容渲染组件（只读）
 * 支持原始 Markdown 文本和 HTML 内容
 */
export function MarkdownViewer(props: { content: string; class?: string }) {
  const [html, setHtml] = createSignal('');

  createEffect(async () => {
    const content = props.content;
    if (!content) {
      setHtml('');
      return;
    }
    // 检测内容是否已经是 HTML（包含常见 HTML 标签）
    if (/<[a-z][\s\S]*>/i.test(content)) {
      setHtml(content);
    } else {
      // 原始 Markdown，使用 marked 解析
      const { marked } = await import('marked');
      setHtml(marked.parse(content, { async: false }) as string);
    }
  });

  return (
    <div
      class={`morty-markdown-viewer ${props.class || ''}`}
      innerHTML={html()}
    />
  );
}
