import type { JSX, ParentComponent } from 'solid-js';
import { splitProps, Show, createSignal, createEffect, onCleanup } from 'solid-js';
import { Portal } from 'solid-js/web';
import { createSafeContext, createDisclosure, getDataState } from '../shared';

export interface DialogRootProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  modal?: boolean;
}

export interface DialogContextValue {
  isOpen: () => boolean;
  setOpen: (open: boolean) => void;
  close: () => void;
  triggerRef: () => HTMLButtonElement | undefined;
  setTriggerRef: (el: HTMLButtonElement) => void;
  contentRef: () => HTMLDivElement | undefined;
  setContentRef: (el: HTMLDivElement) => void;
  modal: () => boolean;
}

const [DialogContext, useDialogContext] = createSafeContext<DialogContextValue>(
  'Dialog components must be used within Dialog.Root'
);

export { useDialogContext };

export const DialogRoot: ParentComponent<DialogRootProps> = (props) => {
  const [local] = splitProps(props, ['open', 'defaultOpen', 'onOpenChange', 'modal', 'children']);

  const [triggerRef, setTriggerRef] = createSignal<HTMLButtonElement>();
  const [contentRef, setContentRef] = createSignal<HTMLDivElement>();

  const disclosure = createDisclosure({
    get open() { return local.open; },
    get defaultOpen() { return local.defaultOpen; },
    get onOpenChange() { return local.onOpenChange; },
  });

  const modal = () => local.modal ?? true;

  createEffect(() => {
    if (disclosure.isOpen() && modal()) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }

    onCleanup(() => {
      document.body.style.overflow = '';
    });
  });

  const context: DialogContextValue = {
    isOpen: disclosure.isOpen,
    setOpen: disclosure.setOpen,
    close: disclosure.close,
    triggerRef,
    setTriggerRef,
    contentRef,
    setContentRef,
    modal,
  };

  return (
    <DialogContext.Provider value={context}>
      {local.children}
    </DialogContext.Provider>
  );
};

export const DialogTrigger: ParentComponent<JSX.ButtonHTMLAttributes<HTMLButtonElement>> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'children', 'onClick']);
  const context = useDialogContext();

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
      aria-haspopup="dialog"
      data-state={getDataState(context.isOpen())}
      {...local}
      {...rest}
    >
      {local.children}
    </button>
  );
};

export const DialogPortal: ParentComponent<{ children?: JSX.Element; forceMount?: boolean }> = (props) => {
  const [local] = splitProps(props, ['children', 'forceMount']);
  const context = useDialogContext();

  return (
    <Show when={local.forceMount || context.isOpen()}>
      <Portal mount={document.body}>
        {local.children}
      </Portal>
    </Show>
  );
};

export const DialogOverlay: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'onClick']);
  const context = useDialogContext();

  return (
    <div
      data-state={getDataState(context.isOpen())}
      {...local}
      {...rest}
    />
  );
};

export const DialogContent: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement> & {
    onOpenAutoFocus?: (event: Event) => void;
    onCloseAutoFocus?: (event: Event) => void;
    onEscapeKeyDown?: (event: KeyboardEvent) => void;
    onPointerDownOutside?: (event: MouseEvent) => void;
    onInteractOutside?: (event: Event) => void;
  }
> = (props) => {
  const [local, rest] = splitProps(props, [
    'ref',
    'class',
    'children',
    'onOpenAutoFocus',
    'onCloseAutoFocus',
    'onEscapeKeyDown',
    'onPointerDownOutside',
    'onInteractOutside',
  ]);
  const context = useDialogContext();
  let dialogRef: HTMLDivElement | undefined;

  const handleKeyDown = (e: KeyboardEvent & { currentTarget: HTMLDivElement }) => {
    if (e.key === 'Escape') {
      local.onEscapeKeyDown?.(e);
      context.close();
    }

    if (!dialogRef || e.key !== 'Tab') return;

    const focusableElements = dialogRef.querySelectorAll<HTMLElement>(
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

  createEffect(() => {
    if (context.isOpen() && dialogRef) {
      const focusableElements = dialogRef.querySelectorAll<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
      );
      const firstElement = focusableElements[0];
      firstElement?.focus();
      local.onOpenAutoFocus?.(new Event('focus'));
    }
  });

  return (
    <div
      ref={(el) => {
        dialogRef = el;
        context.setContentRef(el);
        if (typeof local.ref === 'function') {
          local.ref(el);
        }
      }}
      role="dialog"
      aria-modal={context.modal()}
      data-state={getDataState(context.isOpen())}
      onKeyDown={handleKeyDown}
      {...local}
      {...rest}
    >
      {local.children}
    </div>
  );
};

export const DialogHeader: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return <div {...local} {...rest} />;
};

export const DialogTitle: ParentComponent<JSX.HTMLAttributes<HTMLHeadingElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'id']);
  return <h2 id={local.id} role="heading" aria-level="2" {...local} {...rest} />;
};

export const DialogDescription: ParentComponent<JSX.HTMLAttributes<HTMLParagraphElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'id']);
  return <p id={local.id} {...local} {...rest} />;
};

export const DialogFooter: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class']);
  return <div {...local} {...rest} />;
};

export const DialogClose: ParentComponent<JSX.ButtonHTMLAttributes<HTMLButtonElement>> = (props) => {
  const [local, rest] = splitProps(props, ['ref', 'children', 'onClick']);
  const context = useDialogContext();

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

export const Dialog = {
  Root: DialogRoot,
  Trigger: DialogTrigger,
  Portal: DialogPortal,
  Overlay: DialogOverlay,
  Content: DialogContent,
  Header: DialogHeader,
  Title: DialogTitle,
  Description: DialogDescription,
  Footer: DialogFooter,
  Close: DialogClose,
};
