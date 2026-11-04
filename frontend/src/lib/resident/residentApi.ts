import { apiClient } from '../api/client';
import { API_ENDPOINTS } from '../api/endpoints';
import { ensureCsrfToken } from '../auth/authApi';
import type {
  AmenityBookedSlot,
  ResidentAmenity,
  ResidentAmenityBooking,
  ResidentAnnouncement,
  ResidentApartmentOption,
  ResidentApartmentResponse,
  ResidentComplaint,
  ResidentDashboardResponse,
  ResidentInvoiceDetailsResponse,
  ResidentInvoicesListResponse,
  ResidentNotificationItem,
  ResidentProfileResponse,
  ResidentRepairDetails,
  ResidentRepairSummary,
  ResidentVehicle,
  ResidentVisitor,
} from '../../types/resident';

export const residentApi = {
  getDashboard() {
    return apiClient.get<ResidentDashboardResponse>(API_ENDPOINTS.resident.dashboard);
  },

  getApartmentAndContracts() {
    return apiClient.get<ResidentApartmentResponse>(API_ENDPOINTS.resident.apartment);
  },

  getInvoices() {
    return apiClient.get<ResidentInvoicesListResponse>(API_ENDPOINTS.resident.invoices);
  },

  getInvoiceDetails(id: number) {
    return apiClient.get<ResidentInvoiceDetailsResponse>(
      API_ENDPOINTS.resident.invoiceDetails(id)
    );
  },

  async createPaymentAttempt(invoiceId: number) {
    await ensureCsrfToken();
    return apiClient.post<{
      message: string;
      invoice: ResidentInvoiceDetailsResponse;
    }>(API_ENDPOINTS.resident.createPaymentAttempt(invoiceId));
  },

  getAmenities() {
    return apiClient.get<{
      amenities: ResidentAmenity[];
      myBookings: ResidentAmenityBooking[];
    }>(API_ENDPOINTS.resident.amenities);
  },

  getAmenityBookedSlots(amenityId: number, date: string) {
    return apiClient.get<AmenityBookedSlot[]>(
      API_ENDPOINTS.resident.amenityBookedSlots(amenityId, date)
    );
  },

  async createAmenityBooking(payload: {
    amenityId: number;
    bookingDate: string;
    startTime: string;
    endTime: string;
    numberOfGuests: number;
    notes?: string;
  }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.createAmenityBooking,
      payload
    );
  },

  async cancelAmenityBooking(bookingId: number) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.cancelAmenityBooking(bookingId)
    );
  },

  getVehicles(params?: { searchTerm?: string; status?: string }) {
    const query = new URLSearchParams();
    if (params?.searchTerm) query.set('searchTerm', params.searchTerm);
    if (params?.status) query.set('status', params.status);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiClient.get<{
      vehicles: ResidentVehicle[];
      apartments: ResidentApartmentOption[];
    }>(`${API_ENDPOINTS.resident.vehicles}${suffix}`);
  },

  async registerVehicle(payload: {
    vehicleType: string;
    licensePlate: string;
    brandModel?: string;
    color?: string;
  }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.vehicles,
      payload
    );
  },

  async cancelVehicle(vehicleId: number) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.cancelVehicle(vehicleId)
    );
  },

  async lockVehicleCard(vehicleId: number, reason?: string) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.lockVehicleCard(vehicleId),
      { reason }
    );
  },

  async decommissionVehicle(vehicleId: number, reason?: string) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.decommissionVehicle(vehicleId),
      { reason }
    );
  },

  getVisitors() {
    return apiClient.get<{
      visitors: ResidentVisitor[];
      apartments: ResidentApartmentOption[];
    }>(API_ENDPOINTS.resident.visitors);
  },

  async registerVisitor(payload: {
    apartmentId: number;
    visitorName: string;
    phoneNumber?: string;
    idCardNumber?: string;
    expectedArrivalDate: string;
    purpose?: string;
  }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.visitors,
      payload
    );
  },

  async cancelVisitor(visitorPassId: number) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.cancelVisitor(visitorPassId)
    );
  },

  getRepairs() {
    return apiClient.get<{
      repairs: ResidentRepairSummary[];
      apartments: ResidentApartmentOption[];
    }>(API_ENDPOINTS.resident.repairs);
  },

  getRepairDetails(id: number) {
    return apiClient.get<ResidentRepairDetails>(
      API_ENDPOINTS.resident.repairDetails(id)
    );
  },

  async createRepair(payload: {
    apartmentId: number;
    title: string;
    description: string;
    priority: string;
  }) {
    await ensureCsrfToken();
    return apiClient.post<{ requestId: number; message: string }>(
      API_ENDPOINTS.resident.repairs,
      payload
    );
  },

  async cancelRepair(id: number, payload: { reason?: string; rowVersionBase64?: string }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.cancelRepair(id),
      payload
    );
  },

  async confirmRepair(
    id: number,
    payload: { rating: number; feedback?: string; rowVersionBase64?: string }
  ) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.confirmRepair(id),
      payload
    );
  },

  async redoRepair(id: number, payload: { redoReason: string; rowVersionBase64?: string }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.redoRepair(id),
      payload
    );
  },

  getComplaints(params?: { searchTerm?: string; status?: string; category?: string }) {
    const query = new URLSearchParams();
    if (params?.searchTerm) query.set('searchTerm', params.searchTerm);
    if (params?.status) query.set('status', params.status);
    if (params?.category) query.set('category', params.category);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiClient.get<{
      complaints: ResidentComplaint[];
      apartments: ResidentApartmentOption[];
    }>(`${API_ENDPOINTS.resident.complaints}${suffix}`);
  },

  getComplaintDetails(id: number) {
    return apiClient.get<ResidentComplaint>(API_ENDPOINTS.resident.complaintDetails(id));
  },

  async createComplaint(payload: {
    title: string;
    category: string;
    priority: string;
    apartmentId?: number | null;
    content: string;
  }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.complaints,
      payload
    );
  },

  async updateComplaint(
    id: number,
    payload: {
      complaintId: number;
      title: string;
      category: string;
      priority: string;
      apartmentId?: number | null;
      content: string;
    }
  ) {
    await ensureCsrfToken();
    return apiClient.put<{ message: string }>(
      API_ENDPOINTS.resident.complaintDetails(id),
      payload
    );
  },

  async cancelComplaint(id: number) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.cancelComplaint(id)
    );
  },

  async rateComplaint(id: number, payload: { rating: number; feedback?: string }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.rateComplaint(id),
      payload
    );
  },

  getNotifications(page = 1, unreadOnly?: boolean) {
    return apiClient.get<{
      page: number;
      unreadCount: number;
      notifications: ResidentNotificationItem[];
      announcements: ResidentAnnouncement[];
    }>(API_ENDPOINTS.resident.notifications(page, unreadOnly));
  },

  async markNotificationRead(id: number) {
    await ensureCsrfToken();
    return apiClient.post<{ success: boolean; unreadCount: number }>(
      API_ENDPOINTS.notifications.markRead(id)
    );
  },

  async markAllNotificationsRead() {
    await ensureCsrfToken();
    return apiClient.post<{ success: boolean; unreadCount: number }>(
      API_ENDPOINTS.notifications.markAllRead
    );
  },

  async deleteNotification(id: number) {
    await ensureCsrfToken();
    return apiClient.delete<{ message: string; unreadCount: number }>(
      API_ENDPOINTS.resident.deleteNotification(id)
    );
  },

  getProfile() {
    return apiClient.get<ResidentProfileResponse>(API_ENDPOINTS.resident.profile);
  },

  async updateProfile(payload: {
    dateOfBirth?: string | null;
    gender?: string;
    address?: string;
    emergencyContact?: string;
  }) {
    await ensureCsrfToken();
    return apiClient.put<{ message: string }>(API_ENDPOINTS.resident.profile, payload);
  },

  async changePassword(payload: { currentPassword: string; newPassword: string }) {
    await ensureCsrfToken();
    return apiClient.post<{ message: string }>(
      API_ENDPOINTS.resident.changePassword,
      payload
    );
  },
};

