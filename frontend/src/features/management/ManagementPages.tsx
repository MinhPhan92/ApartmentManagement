import { PortalPlaceholderPage } from '../../components/shared/PortalPlaceholderPage';
import { MANAGEMENT_NAV_GROUPS } from '../../lib/auth/permissions';

const ALL_MANAGEMENT_ITEMS = MANAGEMENT_NAV_GROUPS.flatMap((g) => g.items);

export function ManagementRoutePage({ itemId }: { itemId: string }) {
  const item =
    ALL_MANAGEMENT_ITEMS.find((entry) => entry.id === itemId) ??
    ALL_MANAGEMENT_ITEMS[0];

  return (
    <PortalPlaceholderPage
      title={item.label}
      description={item.description}
      iconName={item.iconName}
      portalLabel="Cổng Ban Quản Lý"
      policyLabel={item.policy ?? 'Management Authenticated'}
      isDashboardSkeleton={item.id === 'mgmt-dashboard'}
    />
  );
}
