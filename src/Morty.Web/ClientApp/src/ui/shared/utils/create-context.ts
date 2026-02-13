import { createContext, useContext } from 'solid-js';

export function createSafeContext<T>(errorMessage: string) {
  const Context = createContext<T | undefined>();

  function useSafeContext(): T {
    const context = useContext(Context);
    if (context === undefined) {
      throw new Error(errorMessage);
    }
    return context;
  }

  return [Context, useSafeContext] as const;
}
