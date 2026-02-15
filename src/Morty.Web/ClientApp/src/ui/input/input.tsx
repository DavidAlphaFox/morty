/**
 * Input 输入框组件
 * 标准化的文本输入组件
 */

import type { JSX } from 'solid-js';
import { splitProps } from 'solid-js';

/**
 * Input 组件属性
 * 继承自 HTML input 元素的所有属性
 */
export interface InputProps extends Omit<JSX.InputHTMLAttributes<HTMLInputElement>, 'class'> {
  /** 额外的 CSS 类名 */
  class?: string;
}

/**
 * 输入框组件
 * @param props 输入框属性
 * @returns JSX input 元素
 */
export const Input = (props: InputProps) => {
  // 分离本地属性和传递给原生 input 的属性
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
