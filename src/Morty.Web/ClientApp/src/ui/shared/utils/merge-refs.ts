import { Accessor } from 'solid-js';

export function mergeRefs<T>(...refs: Array<(instance: T | null) => void | null | undefined>) {
  return (instance: T | null) => {
    for (const ref of refs) {
      if (typeof ref === 'function') {
        ref(instance);
      }
    }
  };
}

export function mergeRefAccessors<T>(...refs: Array<Accessor<T | undefined>>) {
  return (value: T) => {
    for (const ref of refs) {
      if (typeof ref === 'function') {
        (ref as (value: T) => void)(value);
      }
    }
  };
}
