export type AppRole =
  | 'SuperAdmin'
  | 'BuildingManager'
  | 'Accountant'
  | 'Technician'
  | 'Resident';

export type AppPolicy =
  | 'RequireSuperAdmin'
  | 'RequireManagement'
  | 'RequireAccountantOrManager'
  | 'RequireTechnician'
  | 'RequireTechnicianOrManagement'
  | 'RequireResident';

export type PortalType = 'resident' | 'management';

export interface UserPermissions {
  requireSuperAdmin: boolean;
  requireManagement: boolean;
  requireAccountantOrManager: boolean;
  requireTechnician: boolean;
  requireTechnicianOrManagement: boolean;
  requireResident: boolean;
}

export interface ResidentSessionContext {
  residentId: number;
  activeApartmentId: number | null;
  activeApartmentCode: string | null;
  buildingName: string | null;
}

export interface CurrentUser {
  userId: string;
  userName: string;
  displayName: string;
  email: string | null;
  phoneNumber: string | null;
  roles: AppRole[];
  portal: PortalType;
  defaultRoute: string;
  permissions: UserPermissions;
  residentContext: ResidentSessionContext | null;
}

export interface CsrfTokenResponse {
  headerName: string;
  requestToken: string;
}

export interface LoginPayload {
  userNameOrEmail: string;
  password: string;
  rememberMe: boolean;
}

export interface LogoutResponse {
  message: string;
}

export type SessionStatus =
  | 'initializing'
  | 'authenticated'
  | 'unauthenticated'
  | 'network_error';

