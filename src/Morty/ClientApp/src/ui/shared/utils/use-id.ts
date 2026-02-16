import type { Accessor } from 'solid-js';
import { createSignal } from 'solid-js';

let id = 0;

export function generateId(prefix = 'solid-ui'): string {
  return `${prefix}-${++id}`;
}

export function useId(prefix?: string): Accessor<string> {
  const [id] = createSignal(generateId(prefix));
  return id;
}
