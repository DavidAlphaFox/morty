/**
 * Button 按钮组件
 * 提供多种样式变体的按钮组件
 */

import type { JSX, ParentComponent } from 'solid-js';
import { splitProps } from 'solid-js';

/**
 * Button 组件属性
 * 继承自 HTML button 元素的所有属性
 */
export interface ButtonProps extends Omit<JSX.ButtonHTMLAttributes<HTMLButtonElement>, 'class'> {
  /** 按钮样式变体 */
  variant?: 'default' | 'primary' | 'secondary' | 'destructive' | 'outline' | 'ghost' | 'link';
  /** 按钮尺寸 */
  size?: 'sm' | 'md' | 'lg';
  /** 额外的 CSS 类名 */
  class?: string;
}

/**
 * 按钮组件
 * @param props 按钮属性
 * @returns JSX 按钮元素
 */
export const Button: ParentComponent<ButtonProps> = (props) => {
  // 分离本地属性和传递给原生 button 的属性
  const [local, rest] = splitProps(props, ['variant', 'size', 'class', 'children']);

  // 获取变体，默认值为 'default'
  const variant = () => local.variant ?? 'default';
  // 获取尺寸，默认值为 'md'
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
