import React from 'react';

export type BadgeVariant =
  | 'default'
  | 'neutral'
  | 'primary'
  | 'info'
  | 'navy'
  | 'success'
  | 'warning'
  | 'danger';

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
}

const badgeStyles: Record<BadgeVariant, string> = {
  default: 'bg-slate-100 text-slate-700 border-slate-200',
  neutral: 'bg-slate-100 text-slate-700 border-slate-200',
  primary: 'bg-blue-50 text-[#2563EB] border-blue-200',
  info: 'bg-blue-50 text-[#2563EB] border-blue-200',
  navy: 'bg-slate-900 text-white border-slate-800',
  success: 'bg-green-50 text-[#16A34A] border-green-200',
  warning: 'bg-amber-50 text-[#D97706] border-amber-200',
  danger: 'bg-red-50 text-[#DC2626] border-red-200',
};

export function Badge({
  variant = 'default',
  className = '',
  children,
  ...rest
}: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center px-2.5 py-0.5 rounded-md text-xs font-medium border ${badgeStyles[variant]} ${className}`}
      {...rest}
    >
      {children}
    </span>
  );
}

