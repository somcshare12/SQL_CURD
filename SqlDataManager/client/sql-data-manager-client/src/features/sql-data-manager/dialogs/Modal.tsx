import { useEffect, useRef, type ReactNode } from 'react';

interface ModalProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** A destructive modal gets a red accent on its title. */
  destructive?: boolean;
  width?: number;
}

/**
 * A reusable accessible modal. It:
 *  - closes on Escape,
 *  - traps initial focus inside the dialog,
 *  - restores focus to the previously-focused element on close,
 *  - is labelled by its title for screen readers.
 * These behaviours satisfy the accessibility rules in the specification.
 */
export function Modal({ title, onClose, children, footer, destructive, width = 560 }: ModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const previouslyFocused = useRef<HTMLElement | null>(null);

  useEffect(() => {
    previouslyFocused.current = document.activeElement as HTMLElement | null;
    dialogRef.current?.focus();

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onClose();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previouslyFocused.current?.focus();
    };
  }, [onClose]);

  return (
    <div className="sdm-modal__overlay" onMouseDown={onClose}>
      <div
        className="sdm-modal"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        ref={dialogRef}
        style={{ maxWidth: width }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header className={`sdm-modal__header ${destructive ? 'is-destructive' : ''}`}>
          <h2>{title}</h2>
          <button type="button" className="sdm-modal__close" aria-label="Close dialog" onClick={onClose}>
            ×
          </button>
        </header>
        <div className="sdm-modal__body">{children}</div>
        {footer && <footer className="sdm-modal__footer">{footer}</footer>}
      </div>
    </div>
  );
}
