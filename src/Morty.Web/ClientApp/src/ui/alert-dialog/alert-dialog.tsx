import type { ParentComponent, JSX } from 'solid-js';
import { splitProps } from 'solid-js';
import { DialogRoot, DialogTrigger, DialogPortal, DialogContent, DialogTitle, DialogDescription, DialogClose } from '../dialog/dialog';

export interface AlertDialogProps {
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}

export const AlertDialog: ParentComponent<AlertDialogProps> = (props) => {
  const [local, rest] = splitProps(props, ['children']);

  return (
    <DialogRoot modal {...rest}>
      {local.children}
    </DialogRoot>
  );
};

export const AlertDialogTrigger = DialogTrigger;
export const AlertDialogPortal = DialogPortal;

export const AlertDialogOverlay: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  return <div class="happy-alert-dialog-overlay" {...props} />;
};

export const AlertDialogContent: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['children']);

  return (
    <DialogPortal>
      <div class="happy-alert-dialog-overlay" />
      <DialogContent class="happy-alert-dialog-content" {...rest}>
        {local.children}
      </DialogContent>
    </DialogPortal>
  );
};

export const AlertDialogHeader: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  return <div class="happy-alert-dialog-header" {...props} />;
};

export const AlertDialogFooter: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  return <div class="happy-alert-dialog-footer" {...props} />;
};

export const AlertDialogTitle = DialogTitle;
export const AlertDialogDescription = DialogDescription;
export const AlertDialogClose = DialogClose;
export const AlertDialogAction = DialogClose;
