import type { JSX, ParentComponent } from 'solid-js';
import { splitProps } from 'solid-js';

export type BadgeVariant =
  | 'default'
  | 'secondary'
  | 'destructive'
  | 'outline'
  | 'ghost'
  | 'link';

export interface BadgeProps extends JSX.HTMLAttributes<HTMLDivElement> {
  variant?: BadgeVariant;
  class?: string;
}

export const Badge: ParentComponent<BadgeProps> = (props) => {
  const [local, rest] = splitProps(props, ['variant', 'class', 'children']);

  const variant = local.variant ?? 'default';

  return (
    <div
      classList={{
        'happy-badge': true,
        [local.class || '']: !!local.class,
      }}
      data-variant={variant}
      {...rest}
    >
      {local.children}
    </div>
  );
};
