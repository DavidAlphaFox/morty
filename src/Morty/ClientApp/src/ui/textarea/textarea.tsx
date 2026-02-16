import type { JSX } from 'solid-js';
import { splitProps } from 'solid-js';

export interface TextareaProps extends Omit<JSX.TextareaHTMLAttributes<HTMLTextAreaElement>, 'class'> {
  class?: string;
}

export const Textarea = (props: TextareaProps) => {
  const [local, rest] = splitProps(props, ['class']);

  return (
    <textarea
      classList={{
        'happy-textarea': true,
        [local.class || '']: !!local.class,
      }}
      {...rest}
    />
  );
};
