import { createSignal, createEffect, onCleanup, Accessor } from 'solid-js';

export interface UseDisclosureOptions {
  defaultOpen?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  closeOnEscape?: boolean;
  closeOnOutsideClick?: boolean;
}

export interface UseDisclosureReturn {
  isOpen: Accessor<boolean>;
  setOpen: (open: boolean) => void;
  toggle: () => void;
  open: () => void;
  close: () => void;
}

export function createDisclosure(options: UseDisclosureOptions = {}): UseDisclosureReturn {
  const [uncontrolledOpen, setUncontrolledOpen] = createSignal(options.defaultOpen ?? false);

  const isOpen = () => options.open ?? uncontrolledOpen();

  const setOpen = (open: boolean) => {
    if (options.open === undefined) {
      setUncontrolledOpen(open);
    }
    options.onOpenChange?.(open);
  };

  const toggle = () => setOpen(!isOpen());
  const open = () => setOpen(true);
  const close = () => setOpen(false);

  return { isOpen, setOpen, toggle, open, close };
}

export function useInteractiveDisclosure(
  options: UseDisclosureOptions & {
    contentRef: Accessor<HTMLElement | undefined>;
    triggerRef?: Accessor<HTMLElement | undefined>;
  } = {} as any
): UseDisclosureReturn {
  const disclosure = createDisclosure(options);
  const { contentRef, triggerRef, closeOnEscape = true, closeOnOutsideClick = true } = options;

  createEffect(() => {
    const content = contentRef();
    if (!disclosure.isOpen() || !content) return;

    if (closeOnOutsideClick) {
      const handleClickOutside = (e: MouseEvent) => {
        const target = e.target as HTMLElement;
        if (!content.contains(target)) {
          const trigger = triggerRef?.();
          if (!trigger || !trigger.contains(target)) {
            disclosure.close();
          }
        }
      };

      queueMicrotask(() => {
        document.addEventListener('click', handleClickOutside);
      });

      onCleanup(() => {
        document.removeEventListener('click', handleClickOutside);
      });
    }

    if (closeOnEscape) {
      const handleEscape = (e: KeyboardEvent) => {
        if (e.key === 'Escape') {
          disclosure.close();
          const trigger = triggerRef?.();
          if (trigger) {
            trigger.focus();
          }
        }
      };

      document.addEventListener('keydown', handleEscape);

      onCleanup(() => {
        document.removeEventListener('keydown', handleEscape);
      });
    }
  });

  return disclosure;
}
