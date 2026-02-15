import type { JSX, ParentComponent } from 'solid-js';
import { splitProps, Show, createSignal } from 'solid-js';
import { createSafeContext, getDataState } from '../shared';

export interface TabsRootProps {
  defaultValue?: string;
  value?: string;
  onChange?: (value: string) => void;
  activationMode?: 'automatic' | 'manual';
  class?: string;
}

export interface TabsContextValue {
  value: () => string | undefined;
  setValue: (value: string) => void;
  activationMode: () => 'automatic' | 'manual';
}

const [TabsContext, useTabsContext] = createSafeContext<TabsContextValue>(
  'Tabs components must be used within Tabs.Root'
);

export { useTabsContext };

export const TabsRoot: ParentComponent<TabsRootProps> = (props) => {
  const [local] = splitProps(props, ['defaultValue', 'value', 'onChange', 'activationMode', 'children', 'class']);

  const [value, setValue] = createSignal(local.value ?? local.defaultValue);

  const handleSetValue = (newValue: string) => {
    setValue(newValue);
    local.onChange?.(newValue);
  };

  const context: TabsContextValue = {
    value: () => local.value ?? value(),
    setValue: handleSetValue,
    activationMode: () => local.activationMode ?? 'automatic',
  };

  return <TabsContext.Provider value={context}><div class={local.class}>{local.children}</div></TabsContext.Provider>;
};

export const TabsList: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);
  return (
    <div
      role="tablist"
      {...local}
      {...rest}
    >
      {local.children}
    </div>
  );
};

export const TabsTrigger: ParentComponent<
  JSX.ButtonHTMLAttributes<HTMLButtonElement> & { value: string }
> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children', 'value', 'disabled']);
  const context = useTabsContext();

  const isActive = () => context.value() === local.value;

  return (
    <button
      role="tab"
      aria-selected={isActive()}
      aria-disabled={local.disabled}
      aria-controls={`${local.value}-panel`}
      id={`${local.value}-trigger`}
      data-state={isActive() ? 'active' : 'inactive'}
      data-disabled={local.disabled ? '' : undefined}
      disabled={local.disabled}
      onClick={() => context.setValue(local.value)}
      {...local}
      {...rest}
    >
      {local.children}
    </button>
  );
};

export const TabsContent: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement> & { value: string }
> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children', 'value']);
  const context = useTabsContext();

  const isActive = () => context.value() === local.value;

  return (
    <Show when={isActive()}>
      <div
        role="tabpanel"
        id={`${local.value}-panel`}
        aria-labelledby={`${local.value}-trigger`}
        data-state={getDataState(isActive())}
        tabindex="0"
        {...local}
        {...rest}
      >
        {local.children}
      </div>
    </Show>
  );
};

export const TabsPanel = TabsContent;

export const Tabs = {
  Root: TabsRoot,
  List: TabsList,
  Trigger: TabsTrigger,
  Content: TabsContent,
  Panel: TabsPanel,
};
