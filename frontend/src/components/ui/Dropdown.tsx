import React, { useEffect, useRef, useState } from 'react';

export interface DropdownProps {
  trigger: React.ReactNode;
  triggerLabel: string;
  align?: 'left' | 'right';
  widthClass?: string;
  children: (helpers: { close: () => void }) => React.ReactNode;
}

export function Dropdown({
  trigger,
  triggerLabel,
  align = 'right',
  widthClass = 'w-64',
  children,
}: DropdownProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative inline-block text-left">
      <button
        type="button"
        aria-label={triggerLabel}
        aria-expanded={open}
        aria-haspopup="menu"
        onClick={() => setOpen((prev) => !prev)}
        className="inline-flex items-center focus:outline-none focus-visible:ring-2 focus-visible:ring-[#2563EB] rounded-md"
      >
        {trigger}
      </button>

      {open && (
        <div
          role="menu"
          className={`absolute z-40 mt-2 ${widthClass} rounded-lg bg-white border border-slate-200 shadow-lg focus:outline-none ${
            align === 'right' ? 'right-0' : 'left-0'
          }`}
        >
          {children({ close: () => setOpen(false) })}
        </div>
      )}
    </div>
  );
}

