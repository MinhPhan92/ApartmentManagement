import type { AppPolicy, AppRole, CurrentUser, PortalType } from '../../types/auth';

export interface PortalNavItem {
  id: string;
  label: string;
  path: string;
  description: string;
  iconName:
    | 'LayoutDashboard'
    | 'Building2'
    | 'Home'
    | 'Users'
    | 'FileText'
    | 'Receipt'
    | 'CreditCard'
    | 'Wrench'
    | 'MessageSquareWarning'
    | 'AlertTriangle'
    | 'Car'
    | 'UserCheck'
    | 'CalendarCheck'
    | 'Bell'
    | 'BarChart3'
    | 'ShieldCheck'
    | 'UserCog';
  policy?: AppPolicy;
  allowedRoles?: AppRole[];
}

export interface PortalNavGroup {
  id: string;
  title: string;
  items: PortalNavItem[];
}

export function hasRole(user: CurrentUser | null, role: AppRole): boolean {
  if (!user) return false;
  return user.roles.includes(role);
}

export function satisfiesPolicy(user: CurrentUser | null, policy?: AppPolicy): boolean {
  if (!user) return false;
  if (!policy) return true;

  switch (policy) {
    case 'RequireSuperAdmin':
      return user.permissions.requireSuperAdmin || hasRole(user, 'SuperAdmin');
    case 'RequireManagement':
      return (
        user.permissions.requireManagement ||
        hasRole(user, 'SuperAdmin') ||
        hasRole(user, 'BuildingManager')
      );
    case 'RequireAccountantOrManager':
      return (
        user.permissions.requireAccountantOrManager ||
        hasRole(user, 'SuperAdmin') ||
        hasRole(user, 'BuildingManager') ||
        hasRole(user, 'Accountant')
      );
    case 'RequireTechnician':
      return user.permissions.requireTechnician || hasRole(user, 'Technician');
    case 'RequireTechnicianOrManagement':
      return (
        user.permissions.requireTechnicianOrManagement ||
        hasRole(user, 'SuperAdmin') ||
        hasRole(user, 'BuildingManager') ||
        hasRole(user, 'Technician')
      );
    case 'RequireResident':
      return user.permissions.requireResident || hasRole(user, 'Resident');
    default:
      return false;
  }
}

export function canAccessNavItem(user: CurrentUser | null, item: PortalNavItem): boolean {
  if (!user) return false;
  if (item.policy && !satisfiesPolicy(user, item.policy)) {
    return false;
  }
  if (item.allowedRoles && item.allowedRoles.length > 0) {
    return item.allowedRoles.some((role) => hasRole(user, role));
  }
  return true;
}

export function canAccessPortal(user: CurrentUser | null, portal: PortalType): boolean {
  if (!user) return false;
  if (portal === 'resident') {
    return satisfiesPolicy(user, 'RequireResident');
  }
  return (
    satisfiesPolicy(user, 'RequireManagement') ||
    satisfiesPolicy(user, 'RequireAccountantOrManager') ||
    satisfiesPolicy(user, 'RequireTechnicianOrManagement')
  );
}

export function getRoleDisplayLabel(user: CurrentUser | null): string {
  if (!user || user.roles.length === 0) return 'Người dùng';
  if (hasRole(user, 'SuperAdmin')) return 'Quản trị hệ thống (SuperAdmin)';
  if (hasRole(user, 'BuildingManager')) return 'Ban quản lý tòa nhà';
  if (hasRole(user, 'Accountant')) return 'Kế toán viên';
  if (hasRole(user, 'Technician')) return 'Kỹ thuật viên';
  if (hasRole(user, 'Resident')) return 'Cư dân';
  return user.roles[0];
}

export const RESIDENT_NAV_ITEMS: PortalNavItem[] = [
  {
    id: 'resident-dashboard',
    label: 'Tổng quan',
    path: '/resident/dashboard',
    description: 'Tổng quan thông tin căn hộ, hóa đơn kỳ gần nhất và trạng thái dịch vụ cư dân.',
    iconName: 'LayoutDashboard',
    policy: 'RequireResident',
  },
  {
    id: 'resident-apartment',
    label: 'Căn hộ của tôi',
    path: '/resident/apartment',
    description: 'Thông tin căn hộ đang cư trú, thành viên hộ gia đình và hợp đồng liên quan.',
    iconName: 'Home',
    policy: 'RequireResident',
  },
  {
    id: 'resident-invoices',
    label: 'Hóa đơn & thanh toán',
    path: '/resident/invoices',
    description: 'Tra cứu hóa đơn hàng tháng, chi tiết phí dịch vụ và thanh toán qua mã VietQR.',
    iconName: 'Receipt',
    policy: 'RequireResident',
  },
  {
    id: 'resident-repairs',
    label: 'Yêu cầu sửa chữa',
    path: '/resident/repairs',
    description: 'Gửi yêu cầu sửa chữa kỹ thuật trong căn hộ, theo dõi tiến độ và nghiệm thu kết quả.',
    iconName: 'Wrench',
    policy: 'RequireResident',
  },
  {
    id: 'resident-complaints',
    label: 'Phản ánh',
    path: '/resident/complaints',
    description: 'Gửi phản ánh, góp ý dịch vụ tới Ban quản lý và đánh giá chất lượng phản hồi.',
    iconName: 'MessageSquareWarning',
    policy: 'RequireResident',
  },
  {
    id: 'resident-vehicles',
    label: 'Phương tiện',
    path: '/resident/vehicles',
    description: 'Đăng ký phương tiện, theo dõi trạng thái thẻ gửi xe và báo khóa thẻ khẩn cấp.',
    iconName: 'Car',
    policy: 'RequireResident',
  },
  {
    id: 'resident-visitors',
    label: 'Khách thăm',
    path: '/resident/visitors',
    description: 'Đăng ký trước thông tin khách đến thăm căn hộ và quản lý lịch hẹn khách.',
    iconName: 'UserCheck',
    policy: 'RequireResident',
  },
  {
    id: 'resident-amenities',
    label: 'Tiện ích',
    path: '/resident/amenities',
    description: 'Xem danh mục tiện ích nội khu, kiểm tra khung giờ trống và đặt lịch sử dụng.',
    iconName: 'CalendarCheck',
    policy: 'RequireResident',
  },
  {
    id: 'resident-notifications',
    label: 'Thông báo',
    path: '/resident/notifications',
    description: 'Trung tâm thông báo cá nhân và bảng tin vận hành từ Ban quản lý chung cư.',
    iconName: 'Bell',
    policy: 'RequireResident',
  },
  {
    id: 'resident-account',
    label: 'Tài khoản',
    path: '/resident/account',
    description: 'Quản lý hồ sơ cá nhân cư dân, thông tin liên hệ khẩn cấp và bảo mật tài khoản.',
    iconName: 'UserCog',
    policy: 'RequireResident',
  },
];

export const MANAGEMENT_NAV_GROUPS: PortalNavGroup[] = [
  {
    id: 'group-overview',
    title: 'Điều hành chung',
    items: [
      {
        id: 'mgmt-dashboard',
        label: 'Tổng quan',
        path: '/management/dashboard',
        description: 'Bảng điều khiển chỉ số vận hành, tài chính và công việc ưu tiên theo vai trò.',
        iconName: 'LayoutDashboard',
        allowedRoles: ['SuperAdmin', 'BuildingManager', 'Accountant', 'Technician'],
      },
    ],
  },
  {
    id: 'group-property',
    title: 'Tài sản & Cư trú',
    items: [
      {
        id: 'mgmt-buildings-apartments',
        label: 'Tòa nhà & căn hộ',
        path: '/management/buildings',
        description: 'Quản lý danh mục tòa nhà, quỹ căn hộ và trạng thái khai thác căn hộ.',
        iconName: 'Building2',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-residents',
        label: 'Cư dân',
        path: '/management/residents',
        description: 'Quản lý hồ sơ cư dân, cấp tài khoản cổng cư dân và lịch sử lưu trú hộ gia đình.',
        iconName: 'Users',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-contracts',
        label: 'Hợp đồng',
        path: '/management/contracts',
        description: 'Quản lý vòng đời hợp đồng căn hộ: Nháp, Hiệu lực, Hoàn tất, Chấm dứt và Hủy.',
        iconName: 'FileText',
        policy: 'RequireManagement',
      },
    ],
  },
  {
    id: 'group-finance',
    title: 'Tài chính & Kế toán',
    items: [
      {
        id: 'mgmt-billing',
        label: 'Hóa đơn',
        path: '/management/billing',
        description: 'Quản lý biểu giá dịch vụ, ghi chỉ số điện nước và phát hành hóa đơn định kỳ.',
        iconName: 'Receipt',
        policy: 'RequireAccountantOrManager',
      },
      {
        id: 'mgmt-payments',
        label: 'Thanh toán & công nợ',
        path: '/management/payments',
        description: 'Đối soát giao dịch thanh toán VietQR, xác nhận thu tiền và theo dõi công nợ.',
        iconName: 'CreditCard',
        policy: 'RequireAccountantOrManager',
      },
      {
        id: 'mgmt-reports',
        label: 'Báo cáo',
        path: '/management/reports',
        description: 'Báo cáo sao kê tài chính tháng, thống kê vận hành và xuất dữ liệu CSV.',
        iconName: 'BarChart3',
        policy: 'RequireAccountantOrManager',
      },
    ],
  },
  {
    id: 'group-operations',
    title: 'Kỹ thuật & Dịch vụ',
    items: [
      {
        id: 'mgmt-repairs',
        label: 'Sửa chữa',
        path: '/management/repairs',
        description: 'Hàng đợi yêu cầu sửa chữa căn hộ, phân công kỹ thuật viên và cập nhật tiến độ.',
        iconName: 'Wrench',
        policy: 'RequireTechnicianOrManagement',
      },
      {
        id: 'mgmt-technical-incidents',
        label: 'Sự cố kỹ thuật',
        path: '/management/technical-incidents',
        description: 'Báo cáo sự cố hạ tầng tòa nhà, tiếp nhận, phân công xử lý và nghiệm thu đóng sự cố.',
        iconName: 'AlertTriangle',
        policy: 'RequireTechnicianOrManagement',
      },
      {
        id: 'mgmt-vehicles',
        label: 'Phương tiện',
        path: '/management/vehicles',
        description: 'Xét duyệt đăng ký phương tiện cư dân, cấp mới, khóa và đổi thẻ bãi đỗ xe.',
        iconName: 'Car',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-visitors',
        label: 'Khách thăm',
        path: '/management/visitors',
        description: 'Kiểm soát an ninh khách ra vào tòa nhà, thực hiện Check-in và Check-out.',
        iconName: 'UserCheck',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-complaints',
        label: 'Phản ánh',
        path: '/management/complaints',
        description: 'Tiếp nhận, phản hồi khiếu nại cư dân và chuyển tiếp sang phiếu sửa chữa kỹ thuật.',
        iconName: 'MessageSquareWarning',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-amenities',
        label: 'Tiện ích',
        path: '/management/amenities',
        description: 'Quản lý danh mục tiện ích nội khu và phê duyệt lịch đăng ký sử dụng của cư dân.',
        iconName: 'CalendarCheck',
        policy: 'RequireManagement',
      },
      {
        id: 'mgmt-notifications',
        label: 'Thông báo',
        path: '/management/notifications',
        description: 'Trung tâm thông báo hệ thống và quản lý bản tin chung cư.',
        iconName: 'Bell',
        allowedRoles: ['SuperAdmin', 'BuildingManager', 'Accountant', 'Technician'],
      },
    ],
  },
  {
    id: 'group-system',
    title: 'Quản trị hệ thống',
    items: [
      {
        id: 'mgmt-accounts',
        label: 'Quản trị tài khoản',
        path: '/management/accounts',
        description: 'Quản lý tài khoản người dùng toàn hệ thống, phân quyền Role và khóa/mở khóa.',
        iconName: 'ShieldCheck',
        policy: 'RequireSuperAdmin',
      },
    ],
  },
];

export function getVisibleResidentNavItems(user: CurrentUser | null): PortalNavItem[] {
  return RESIDENT_NAV_ITEMS.filter((item) => canAccessNavItem(user, item));
}

export function getVisibleManagementNavGroups(user: CurrentUser | null): PortalNavGroup[] {
  return MANAGEMENT_NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => canAccessNavItem(user, item)),
  })).filter((group) => group.items.length > 0);
}

