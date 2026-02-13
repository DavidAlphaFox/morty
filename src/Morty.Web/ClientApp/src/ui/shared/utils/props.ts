import { splitProps } from 'solid-js';

export function useClassProps<
  T extends { class?: string; children?: any },
  K extends Exclude<keyof T, 'class' | 'children'>
>(
  props: T,
  additionalKeys: K[] = [],
  baseClasses: Record<string, boolean> = {}
): [
  Pick<T, 'class' | 'children' | K>,
  Omit<T, 'class' | 'children' | K>,
  () => Record<string, boolean>
] {
  const allKeys = ['class', 'children', ...additionalKeys] as (keyof T)[];
  const [local, rest] = splitProps(props, allKeys);

  const classList = () => ({
    ...baseClasses,
    [(local as any).class || '']: !!(local as any).class,
  });

  return [local as any, rest as any, classList];
}

export function createVariantProps<T extends Record<string, string | undefined>>(
  variants: T
): Record<string, string | undefined> {
  const result: Record<string, string | undefined> = {};

  for (const [key, value] of Object.entries(variants)) {
    if (value !== undefined) {
      result[`data-${key}`] = value;
    }
  }

  return result;
}

export function combineClassList(
  ...classLists: Record<string, boolean>[]
): Record<string, boolean> {
  return Object.assign({}, ...classLists);
}
