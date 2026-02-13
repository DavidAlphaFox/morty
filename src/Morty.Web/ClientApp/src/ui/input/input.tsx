import type { JSX } from 'solid-js';
import { splitProps } from 'solid-js';

export interface InputProps extends Omit<JSX.InputHTMLAttributes<HTMLInputElement>, 'class'> {
  class?: string;
}

export const Input = (props: InputProps) => {
  const [local, rest] = splitProps(props, ['class']);

  return (
    <input
      classList={{
        'happy-input': true,
        [local.class || '']: !!local.class,
      }}
      {...rest}
    />
  );
};
