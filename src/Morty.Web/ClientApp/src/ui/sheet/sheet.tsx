import type { JSX, ParentComponent } from 'solid-js';
import { splitProps, Show, createSignal } from 'solid-js';
import { Portal } from 'solid-js/web';
import { createSafeContext, createDisclosure, getDataState } from '../shared';

export type SheetSide = 'top' | 'right' | 'bottom' | 'left';

export interface SheetRootProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
}

export interface SheetContextValue {
  isOpen: () => boolean;
  setOpen: (open: boolean) => void;
  close: () => void;
  triggerRef: () => HTMLButtonElement | undefined;
  setTriggerRef: (el: HTMLButtonElement) => void;
  contentRef: () => HTMLDivElement | undefined;
  setContentRef: (el: HTMLDivElement) => void;
}

const [SheetContext, useSheetContext] = createSafeContext<SheetContextValue>(
  'Sheet components must be used within Sheet.Root'
);

export { useSheetContext };

export const SheetRoot: ParentComponent<SheetRootProps> = (props) => {
  const [local] = splitProps(props, ['defaultOpen', 'onOpenChange', 'children']);

  const [triggerRef, setTriggerRef] = createSignal<HTMLButtonElement>();
  const [contentRef, setContentRef] = createSignal<HTMLDivElement>();

  const disclosure = createDisclosure({
    get defaultOpen() { return local.defaultOpen; },
    get onOpenChange() { return local.onOpenChange; },
  });

  const context: SheetContextValue = {
    isOpen: disclosure.isOpen,
    setOpen: disclosure.setOpen,
    close: disclosure.close,
    triggerRef,
    setTriggerRef,
    contentRef,
    setContentRef,
  };

  return (
    <SheetContext.Provider value={context}>
      {local.children}
    </SheetContext.Provider>
  );
};

export const SheetTrigger: ParentComponent<JSX.ButtonHTMLAttributes<HTMLButtonElement>> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'children', 'onClick']);
  const context = useSheetContext();

  return (
    <button
      ref={(el) => {
        context.setTriggerRef(el);
        if (typeof local.ref === 'function') {
          local.ref(el);
        }
      }}
      onClick={() => {
        context.setOpen(!context.isOpen());
      }}
      aria-expanded={context.isOpen()}
      aria-controls="sheet-content"
      data-state={getDataState(context.isOpen())}
      {...local}
      {...rest}
    >
      {local.children}
    </button>
  );
};

export const SheetPortal: ParentComponent<{ children?: JSX.Element; forceMount?: boolean }> = (props) => {
  const [local] = splitProps(props, ['children', 'forceMount']);
  const context = useSheetContext();

  return (
    <Show when={local.forceMount || context.isOpen()}>
      <Portal mount={document.body}>
        {local.children}
      </Portal>
    </Show>
  );
};

export const SheetOverlay: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'onClick']);
  const context = useSheetContext();

  return (
    <div
      classList={{
        'happy-sheet-overlay': true,
        [local.class || '']: !!local.class,
      }}
      data-state={getDataState(context.isOpen())}
      onClick={() => context.close()}
      {...local}
      {...rest}
    />
  );
};

export interface SheetContentProps extends JSX.HTMLAttributes<HTMLDivElement> {
  side?: SheetSide;
  onEscapeKeyDown?: (event: KeyboardEvent) => void;
  onPointerDownOutside?: (event: MouseEvent) => void;
  onInteractOutside?: (event: Event) => void;
}

export const SheetContent: ParentComponent<SheetContentProps> = (props) => {
  const [local, rest] = splitProps(props, [
    'ref',
    'class',
    'children',
    'side',
    'onEscapeKeyDown',
    'onPointerDownOutside',
    'onInteractOutside',
  ]);
  const context = useSheetContext();
  let sheetRef: HTMLDivElement | undefined;

  const side = local.side ?? 'right';

  const handleKeyDown = (e: KeyboardEvent & { currentTarget: HTMLDivElement }) => {
    if (e.key === 'Escape') {
      local.onEscapeKeyDown?.(e);
      context.close();
    }

    if (!sheetRef || e.key !== 'Tab') return;

    const focusableElements = sheetRef.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );
    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];

    if (e.shiftKey && document.activeElement === firstElement) {
      e.preventDefault();
      lastElement?.focus();
    } else if (!e.shiftKey && document.activeElement === lastElement) {
      e.preventDefault();
      firstElement?.focus();
    }
  };

  return (
    <Show when={context.isOpen()}>
      <Portal mount={document.body}>
        <div class="happy-sheet-wrapper">
          <SheetOverlay />
          <div
            ref={(el) => {
              sheetRef = el;
              context.setContentRef(el);
              if (typeof local.ref === 'function') {
                local.ref(el);
              }
            }}
            classList={{
              'happy-sheet-content': true,
              [local.class || '']: !!local.class,
            }}
            data-side={side}
            data-open={context.isOpen()}
            data-state={getDataState(context.isOpen())}
            onKeyDown={handleKeyDown}
            {...local}
            {...rest}
          >
            {local.children}
            <button
              onClick={() => context.close()}
              class="happy-sheet-close"
            >
              ✕
            </button>
          </div>
        </div>
      </Portal>
    </Show>
  );
};

export const SheetHeader: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return (
    <div
      classList={{
        'happy-sheet-header': true,
        [local.class || '']: !!local.class,
      }}
      {...local}
      {...rest}
    />
  );
};

export const SheetTitle: ParentComponent<JSX.HTMLAttributes<HTMLHeadingElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'id']);
  return (
    <h2
      id={local.id}
      classList={{
        'happy-sheet-title': true,
        [local.class || '']: !!local.class,
      }}
      role="heading"
      aria-level="2"
      {...local}
      {...rest}
    />
  );
};

export const SheetDescription: ParentComponent<JSX.HTMLAttributes<HTMLParagraphElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'id']);
  return (
    <p
      id={local.id}
      classList={{
        'happy-sheet-description': true,
        [local.class || '']: !!local.class,
      }}
      {...local}
      {...rest}
    />
  );
};

export const SheetFooter: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return (
    <div
      classList={{
        'happy-sheet-footer': true,
        [local.class || '']: !!local.class,
      }}
      {...local}
      {...rest}
    />
  );
};

export const SheetClose: ParentComponent<JSX.ButtonHTMLAttributes<HTMLButtonElement>> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'children', 'onClick']);
  const context = useSheetContext();

  const handleClick = () => {
    context.close();
  };

  return (
    <button
      ref={(el) => {
        if (typeof local.ref === 'function') {
          local.ref(el);
        }
      }}
      onClick={handleClick}
      {...local}
      {...rest}
    >
      {local.children}
    </button>
  );
};

export const Sheet = {
  Root: SheetRoot,
  Trigger: SheetTrigger,
  Portal: SheetPortal,
  Overlay: SheetOverlay,
  Content: SheetContent,
  Header: SheetHeader,
  Title: SheetTitle,
  Description: SheetDescription,
  Footer: SheetFooter,
  Close: SheetClose,
};
