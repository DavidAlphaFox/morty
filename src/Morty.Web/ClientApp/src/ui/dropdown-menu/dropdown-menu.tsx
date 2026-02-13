import type { JSX, ParentComponent } from 'solid-js';
import { splitProps, Show, createSignal } from 'solid-js';
import { Portal } from 'solid-js/web';
import { createSafeContext, useInteractiveDisclosure, useFloatingContent, getDataState } from '../shared';

export interface DropdownMenuRootProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
}

export interface DropdownMenuContextValue {
  isOpen: () => boolean;
  setOpen: (open: boolean) => void;
  close: () => void;
  triggerRef: () => HTMLButtonElement | undefined;
  setTriggerRef: (el: HTMLButtonElement) => void;
  contentRef: () => HTMLDivElement | undefined;
  setContentRef: (el: HTMLDivElement) => void;
}

const [DropdownMenuContext, useDropdownMenuContext] = createSafeContext<DropdownMenuContextValue>(
  'DropdownMenu components must be used within DropdownMenu.Root'
);

export { useDropdownMenuContext };

export const DropdownMenuRoot: ParentComponent<DropdownMenuRootProps> = (props) => {
  const [local] = splitProps(props, ['defaultOpen', 'onOpenChange', 'children']);

  const [triggerRef, setTriggerRef] = createSignal<HTMLButtonElement>();
  const [contentRef, setContentRef] = createSignal<HTMLDivElement>();

  const disclosure = useInteractiveDisclosure({
    get defaultOpen() { return local.defaultOpen; },
    get onOpenChange() { return local.onOpenChange; },
    get contentRef() { return contentRef; },
    get triggerRef() { return triggerRef; },
  });

  const context: DropdownMenuContextValue = {
    isOpen: disclosure.isOpen,
    setOpen: disclosure.setOpen,
    close: disclosure.close,
    triggerRef,
    setTriggerRef,
    contentRef,
    setContentRef,
  };

  return (
    <DropdownMenuContext.Provider value={context}>
      {local.children}
    </DropdownMenuContext.Provider>
  );
};

export const DropdownMenuTrigger: ParentComponent<JSX.ButtonHTMLAttributes<HTMLButtonElement>> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'children', 'onClick']);
  const context = useDropdownMenuContext();

  const handleClick = (e: MouseEvent & { currentTarget: HTMLButtonElement; target: Element }) => {
    if (typeof local.onClick === 'function') {
      (local.onClick as (e: MouseEvent & { currentTarget: HTMLButtonElement; target: Element }) => void)(e);
    }
    context.setOpen(!context.isOpen());
  };

  return (
    <button
      ref={(el) => {
        context.setTriggerRef(el);
        if (typeof local.ref === 'function') {
          local.ref(el);
        }
      }}
      onClick={handleClick}
      aria-expanded={context.isOpen()}
      aria-haspopup="true"
      data-state={getDataState(context.isOpen())}
      {...rest}
    >
      {local.children}
    </button>
  );
};

export const DropdownMenuContent: ParentComponent<JSX.HTMLAttributes<HTMLDivElement> & { side?: 'top' | 'bottom' | 'left' | 'right' }> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'class', 'children', 'onKeyDown', 'side']);
  const context = useDropdownMenuContext();

  const [contentRef, setContentRef] = createSignal<HTMLDivElement>();

  useFloatingContent({
    triggerRef: context.triggerRef,
    contentRef: contentRef,
    isOpen: context.isOpen,
    get placement() { return local.side === 'top' ? 'top' : 'bottom'; },
  });

  return (
    <Show when={context.isOpen()}>
      <Portal mount={document.body}>
        <div
          ref={(el) => {
            setContentRef(el);
            context.setContentRef(el);
            if (typeof local.ref === 'function') {
              local.ref(el);
            }
          }}
          data-state={getDataState(context.isOpen())}
          {...rest}
        >
          {local.children}
        </div>
      </Portal>
    </Show>
  );
};

export const DropdownMenuItem: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement> & { disabled?: boolean; textValue?: string }
> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children', 'onClick', 'disabled', 'textValue']);
  const context = useDropdownMenuContext();

  const handleClick = (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => {
    if (local.disabled) return;
    if (typeof local.onClick === 'function') {
      (local.onClick as (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => void)(e);
    }
    context.close();
  };

  return (
    <div
      role="menuitem"
      aria-disabled={local.disabled}
      data-disabled={local.disabled ? '' : undefined}
      onClick={handleClick}
      data-text-value={local.textValue}
      {...rest}
    >
      {local.children}
    </div>
  );
};

export const DropdownMenuSeparator: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return <div role="separator" aria-orientation="horizontal" {...local} {...rest} />;
};

export const DropdownMenuLabel: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return <div role="presentation" {...local} {...rest} />;
};

export const DropdownMenuCheckboxItem: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement> & { checked?: boolean; onCheckedChange?: (checked: boolean) => void }
> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children', 'checked', 'onCheckedChange', 'onClick']);

  const handleClick = (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => {
    if (typeof local.onClick === 'function') {
      (local.onClick as (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => void)(e);
    }
    local.onCheckedChange?.(!local.checked);
  };

  return (
    <div
      role="menuitemcheckbox"
      aria-checked={local.checked}
      data-state={local.checked ? 'checked' : 'unchecked'}
      onClick={handleClick}
      {...local}
      {...rest}
    >
      {local.children}
    </div>
  );
};

export const DropdownMenuRadioGroup: ParentComponent<JSX.HTMLAttributes<HTMLDivElement> & { value?: string; onValueChange?: (value: string) => void }> = (props) => {
  const [local, rest] = splitProps(props, ['value', 'onValueChange', 'children']);
  return <div role="group" data-value={local.value} {...local} {...rest} />;
};

export const DropdownMenuRadioItem: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement> & { value: string }
> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children', 'value', 'onClick']);
  const context = useDropdownMenuContext();

  const handleClick = (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => {
    if (typeof local.onClick === 'function') {
      (local.onClick as (e: MouseEvent & { currentTarget: HTMLDivElement; target: Element }) => void)(e);
    }
    const group = (e.currentTarget as HTMLElement).closest('[role="group"]');
    const onValueChange = (group as any)?.onValueChange;
    if (onValueChange) {
      onValueChange(local.value);
    }
    context.close();
  };

  return (
    <div
      role="menuitemradio"
      aria-checked={false}
      data-state="unchecked"
      data-value={local.value}
      onClick={handleClick}
      {...local}
      {...rest}
    >
      {local.children}
    </div>
  );
};

export const DropdownMenu = {
  Root: DropdownMenuRoot,
  Trigger: DropdownMenuTrigger,
  Content: DropdownMenuContent,
  Item: DropdownMenuItem,
  Separator: DropdownMenuSeparator,
  Label: DropdownMenuLabel,
  CheckboxItem: DropdownMenuCheckboxItem,
  RadioGroup: DropdownMenuRadioGroup,
  RadioItem: DropdownMenuRadioItem,
};
