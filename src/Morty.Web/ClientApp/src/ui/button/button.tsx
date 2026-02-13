import type { JSX, ParentComponent } from 'solid-js';
import { splitProps } from 'solid-js';

export interface ButtonProps extends Omit<JSX.ButtonHTMLAttributes<HTMLButtonElement>, 'class'> {
  variant?: 'default' | 'primary' | 'secondary' | 'destructive' | 'outline' | 'ghost' | 'link';
  size?: 'sm' | 'md' | 'lg';
  class?: string;
}

export const Button: ParentComponent<ButtonProps> = (props) => {
  const [local, rest] = splitProps(props, ['variant', 'size', 'class', 'children']);

  const variant = () => local.variant ?? 'default';
  const size = () => local.size ?? 'md';

  return (
    <button
      classList={{
        'happy-button': true,
        [local.class || '']: !!local.class,
      }}
      data-variant={variant()}
      data-size={size()}
      {...rest}
    >
      {local.children}
    </button>
  );
};
